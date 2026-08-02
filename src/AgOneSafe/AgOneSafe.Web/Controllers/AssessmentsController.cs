using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Assessments;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Infrastructure.Workers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class AssessmentDetailsViewModel
{
    public required Assessment Assessment { get; init; }
    public IReadOnlyList<ControlResult> Results { get; init; } = Array.Empty<ControlResult>();
    public ControlOutcome? OutcomeFilter { get; init; }
}

public class AssessmentsController : AgControllerBase
{
    private readonly IScanQueue _scanQueue;

    public AssessmentsController(IAgOneSafeDbContext db, IScanQueue scanQueue) : base(db) =>
        _scanQueue = scanQueue;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        ViewBag.ScanRunning = _scanQueue.IsRunning(tenant.Id);

        var assessments = await Db.Assessments
            .AsNoTracking()
            .Where(a => a.TenantConnectionId == tenant.Id)
            .OrderByDescending(a => a.StartedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return View(assessments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> Run(ProfileLevel? targetLevel, CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        if (_scanQueue.IsRunning(tenant.Id))
        {
            TempData["Toast"] = "A scan is already running for this tenant.";
            return RedirectToAction(nameof(Index));
        }

        _scanQueue.Enqueue(new AssessmentRequest
        {
            TenantConnectionId = tenant.Id,
            TargetLevel = targetLevel ?? tenant.TargetLevel,
            Trigger = AssessmentTrigger.Manual,
            TriggeredBy = CurrentActor.Name
        });

        TempData["Toast"] = "Baseline scan queued. Results appear here as controls complete.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, ControlOutcome? outcome, CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);

        var assessment = await Db.Assessments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (assessment is null)
        {
            return NotFound();
        }

        var query = Db.ControlResults
            .AsNoTracking()
            .Include(r => r.ControlDefinition)
            .Where(r => r.AssessmentId == id);

        if (outcome is { } filter)
        {
            query = query.Where(r => r.Outcome == filter);
        }

        var results = await query
            .OrderBy(r => r.Outcome == ControlOutcome.Fail ? 0 : r.Outcome == ControlOutcome.Error ? 1 : 2)
            .ThenByDescending(r => r.ControlDefinition!.Severity)
            .ThenBy(r => r.ControlDefinition!.VendorReference)
            .ToListAsync(cancellationToken);

        return View(new AssessmentDetailsViewModel
        {
            Assessment = assessment,
            Results = results,
            OutcomeFilter = outcome
        });
    }

    /// <summary>Polled by the scan page so progress updates without a full reload.</summary>
    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return Json(new { running = false });
        }

        var latest = await Db.Assessments
            .AsNoTracking()
            .Where(a => a.TenantConnectionId == tenant.Id)
            .OrderByDescending(a => a.Id)
            .Select(a => new
            {
                a.Id,
                Status = a.Status.ToString(),
                a.PassedCount,
                a.FailedCount,
                a.TotalControls,
                a.PostureScore
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Json(new { running = _scanQueue.IsRunning(tenant.Id), latest });
    }
}

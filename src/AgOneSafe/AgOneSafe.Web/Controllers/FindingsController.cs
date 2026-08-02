using System.Text.Json;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Agent;
using AgOneSafe.Application.Evaluation;
using AgOneSafe.Application.Remediation;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Domain.Remediation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class FindingsFilter
{
    public Severity? Severity { get; set; }
    public FindingStatus? Status { get; set; }
    public string? Domain { get; set; }
    public bool OpenOnly { get; set; } = true;
}

public class FindingsIndexViewModel
{
    public FindingsFilter Filter { get; init; } = new();
    public IReadOnlyList<Finding> Findings { get; init; } = Array.Empty<Finding>();
    public IReadOnlyList<string> Domains { get; init; } = Array.Empty<string>();
}

public class FindingDetailsViewModel
{
    public required Finding Finding { get; init; }
    public IReadOnlyList<AssertionOutcome> Assertions { get; init; } = Array.Empty<AssertionOutcome>();
    public AgentPlan? Plan { get; init; }
    public RemediationTask? ActiveTask { get; init; }
    public string EvidenceJson { get; init; } = "{}";
}

public class FindingsController : AgControllerBase
{
    private readonly IRemediationAgent _agent;
    private readonly IRemediationService _remediation;

    public FindingsController(IAgOneSafeDbContext db, IRemediationAgent agent, IRemediationService remediation)
        : base(db)
    {
        _agent = agent;
        _remediation = remediation;
    }

    public async Task<IActionResult> Index(FindingsFilter filter, CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        var query = Db.Findings
            .AsNoTracking()
            .Include(f => f.ControlDefinition)
            .Where(f => f.TenantConnectionId == tenant.Id);

        if (filter.OpenOnly)
        {
            query = query.Where(f => f.Status != FindingStatus.Verified && f.Status != FindingStatus.RiskAccepted);
        }

        if (filter.Severity is { } severity)
        {
            query = query.Where(f => f.Severity == severity);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(f => f.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Domain))
        {
            query = query.Where(f => f.ControlDefinition!.Domain == filter.Domain);
        }

        return View(new FindingsIndexViewModel
        {
            Filter = filter,
            Findings = await query
                .OrderByDescending(f => f.RiskScore)
                .ThenByDescending(f => f.Severity)
                .ToListAsync(cancellationToken),
            Domains = await Db.ControlDefinitions
                .AsNoTracking()
                .Select(c => c.Domain)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync(cancellationToken)
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);

        var finding = await Db.Findings
            .AsNoTracking()
            .Include(f => f.ControlDefinition)
            .ThenInclude(c => c!.Actions)
            .ThenInclude(a => a.Assertions)
            .Include(f => f.LatestControlResult)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (finding is null)
        {
            return NotFound();
        }

        var assertions = Array.Empty<AssertionOutcome>();
        var evidence = "{}";

        if (finding.LatestControlResult is { } result)
        {
            evidence = Prettify(result.EvidenceJson);

            try
            {
                assertions = JsonSerializer.Deserialize<AssertionOutcome[]>(result.AssertionResultsJson)
                             ?? Array.Empty<AssertionOutcome>();
            }
            catch (JsonException)
            {
                // Evidence that fails to round-trip is still shown raw below the summary.
            }
        }

        return View(new FindingDetailsViewModel
        {
            Finding = finding,
            Assertions = assertions,
            EvidenceJson = evidence,
            Plan = await _agent.PlanAsync(finding, finding.ControlDefinition!, cancellationToken),
            ActiveTask = await Db.RemediationTasks
                .AsNoTracking()
                .Include(t => t.Executions)
                .Include(t => t.ApprovalRequests)
                .ThenInclude(r => r.Signatures)
                .Where(t => t.FindingId == id)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.Ciso)]
    public async Task<IActionResult> AcceptRisk(int id, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Toast"] = "Risk acceptance needs a written justification.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _remediation.AcceptRiskAsync(id, CurrentActor, reason, cancellationToken);
        TempData["Toast"] = "Residual risk accepted and recorded in the audit log.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static string Prettify(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }
}

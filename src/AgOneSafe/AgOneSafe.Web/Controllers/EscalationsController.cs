using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Escalations;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Escalations;
using AgOneSafe.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class EscalationsIndexViewModel
{
    public IReadOnlyList<Finding> LicenseBlocked { get; init; } = Array.Empty<Finding>();
    public IReadOnlyList<ServiceEscalation> Escalations { get; init; } = Array.Empty<ServiceEscalation>();
}

public class EscalationsController : AgControllerBase
{
    private readonly IEscalationService _escalations;

    public EscalationsController(IAgOneSafeDbContext db, IEscalationService escalations) : base(db) =>
        _escalations = escalations;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        return View(new EscalationsIndexViewModel
        {
            LicenseBlocked = await Db.Findings
                .AsNoTracking()
                .Include(f => f.ControlDefinition)
                .Where(f => f.TenantConnectionId == tenant.Id &&
                            f.ControlDefinition!.RequiredLicense != null &&
                            f.Status != FindingStatus.Verified &&
                            f.Status != FindingStatus.RiskAccepted)
                .OrderByDescending(f => f.RiskScore)
                .ToListAsync(cancellationToken),
            Escalations = await Db.ServiceEscalations
                .AsNoTracking()
                .Include(e => e.Finding)
                .ThenInclude(f => f!.ControlDefinition)
                .Where(e => e.TenantConnectionId == tenant.Id)
                .OrderByDescending(e => e.Id)
                .ToListAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> Raise(
        int? findingId,
        EscalationType type,
        string? requestedSku,
        int seatCount,
        string justification,
        CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        var escalation = await _escalations.RaiseAsync(new EscalationRequest
        {
            TenantConnectionId = tenant.Id,
            FindingId = findingId,
            Type = type,
            RequestedSku = requestedSku,
            SeatCount = seatCount <= 0 ? 1 : seatCount,
            Justification = justification ?? string.Empty,
            RequestedBy = CurrentActor.Name
        }, cancellationToken);

        var submitted = await _escalations.SubmitAsync(escalation.Id, cancellationToken);
        TempData["Toast"] = submitted.ProviderMessage;

        return RedirectToAction(nameof(Index));
    }
}

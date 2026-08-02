using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Remediation;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Domain.Remediation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class ApprovalsController : AgControllerBase
{
    private readonly IRemediationService _remediation;

    public ApprovalsController(IAgOneSafeDbContext db, IRemediationService remediation) : base(db) =>
        _remediation = remediation;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        var tasks = await Db.RemediationTasks
            .AsNoTracking()
            .Include(t => t.ControlDefinition)
            .Include(t => t.ApprovalRequests)
            .ThenInclude(r => r.Signatures)
            .Where(t => t.TenantConnectionId == tenant.Id &&
                        (t.Status == RemediationStatus.PendingApproval ||
                         t.Status == RemediationStatus.Approved ||
                         t.Status == RemediationStatus.Scheduled))
            .OrderByDescending(t => t.ImpactSeverity)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        return View(tasks);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanApprove)]
    public async Task<IActionResult> Decide(
        int id,
        ApprovalDecision decision,
        string? comment,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = await _remediation.RecordDecisionAsync(id, CurrentActor, decision, comment, cancellationToken);

            TempData["Toast"] = request.Decision switch
            {
                ApprovalDecision.Approved => "Approved. The agent may now execute this change.",
                ApprovalDecision.Rejected => "Rejected. The task will not execute.",
                _ =>
                    $"Signature recorded: {request.ApprovedCount} of {request.RequiredSignatures} required. " +
                    "A second, different approver is still needed."
            };
        }
        catch (InvalidOperationException ex)
        {
            TempData["Toast"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

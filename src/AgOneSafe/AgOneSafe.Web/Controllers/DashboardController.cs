using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Assessments;
using AgOneSafe.Application.Models;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Audit;
using AgOneSafe.Domain.Remediation;
using AgOneSafe.Infrastructure.Workers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class DashboardViewModel
{
    public PostureSummary? Posture { get; init; }
    public IReadOnlyList<AuditLogEntry> RecentActivity { get; init; } = Array.Empty<AuditLogEntry>();
    public IReadOnlyList<RemediationTask> AwaitingApproval { get; init; } = Array.Empty<RemediationTask>();
    public int CatalogSize { get; init; }
    public int AutomatedRemediationCount { get; init; }
    public bool ScanRunning { get; init; }
    public bool HasAssessment => Posture?.AssessmentId is not null;
}

public class DashboardController : AgControllerBase
{
    private readonly IPostureService _posture;
    private readonly IScanQueue _scanQueue;

    public DashboardController(IAgOneSafeDbContext db, IPostureService posture, IScanQueue scanQueue)
        : base(db)
    {
        _posture = posture;
        _scanQueue = scanQueue;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        var model = new DashboardViewModel
        {
            Posture = await _posture.GetSummaryAsync(tenant.Id, cancellationToken),
            RecentActivity = await Db.AuditLogEntries
                .AsNoTracking()
                .Where(e => e.TenantConnectionId == tenant.Id)
                .OrderByDescending(e => e.Id)
                .Take(12)
                .ToListAsync(cancellationToken),
            AwaitingApproval = await Db.RemediationTasks
                .AsNoTracking()
                .Include(t => t.ControlDefinition)
                .Where(t => t.TenantConnectionId == tenant.Id && t.Status == RemediationStatus.PendingApproval)
                .OrderByDescending(t => t.ImpactSeverity)
                .Take(5)
                .ToListAsync(cancellationToken),
            CatalogSize = await Db.ControlDefinitions.CountAsync(c => c.IsEnabled, cancellationToken),
            AutomatedRemediationCount = await Db.ControlDefinitions
                .CountAsync(c => c.IsEnabled && c.IsAutomatedRemediation, cancellationToken),
            ScanRunning = _scanQueue.IsRunning(tenant.Id)
        };

        return View(model);
    }
}

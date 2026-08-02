using System.Globalization;
using System.Text;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Audit;
using AgOneSafe.Domain.Remediation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class AuditIndexViewModel
{
    public IReadOnlyList<AuditLogEntry> Entries { get; init; } = Array.Empty<AuditLogEntry>();
    public IReadOnlyList<RemediationTask> ChangeLog { get; init; } = Array.Empty<RemediationTask>();
    public bool ChainIntact { get; init; }
    public long? ChainBrokenAt { get; init; }
    public AuditEventType? EventFilter { get; init; }
}

public class AuditController : AgControllerBase
{
    private readonly IAuditTrail _audit;

    public AuditController(IAgOneSafeDbContext db, IAuditTrail audit) : base(db) => _audit = audit;

    public async Task<IActionResult> Index(AuditEventType? eventType, CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        var query = Db.AuditLogEntries
            .AsNoTracking()
            .Where(e => e.TenantConnectionId == tenant.Id);

        if (eventType is { } filter)
        {
            query = query.Where(e => e.EventType == filter);
        }

        var (intact, brokenAt) = await _audit.VerifyChainAsync(cancellationToken);

        return View(new AuditIndexViewModel
        {
            Entries = await query.OrderByDescending(e => e.Id).Take(200).ToListAsync(cancellationToken),
            ChangeLog = await Db.RemediationTasks
                .AsNoTracking()
                .Include(t => t.ControlDefinition)
                .Include(t => t.ApprovalRequests)
                .ThenInclude(r => r.Signatures)
                .Where(t => t.TenantConnectionId == tenant.Id &&
                            (t.Status == RemediationStatus.Succeeded ||
                             t.Status == RemediationStatus.RolledBack ||
                             t.Status == RemediationStatus.Failed))
                .OrderByDescending(t => t.CompletedAt)
                .ToListAsync(cancellationToken),
            ChainIntact = intact,
            ChainBrokenAt = brokenAt,
            EventFilter = eventType
        });
    }

    /// <summary>
    /// Exports the compliance evidence auditors ask for: control, finding, executor, timestamp,
    /// approver and verification status, one row per remediated control.
    /// </summary>
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        var tasks = await Db.RemediationTasks
            .AsNoTracking()
            .Include(t => t.ControlDefinition)
            .Include(t => t.Finding)
            .Include(t => t.Executions)
            .Include(t => t.ApprovalRequests)
            .ThenInclude(r => r.Signatures)
            .Where(t => t.TenantConnectionId == tenant.Id)
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine(
            "ControlId,Title,Domain,Severity,Finding,Executor,ExecutedAtUtc,Status,Approvers,SignaturesRequired,VerificationStatus,SnapshotId,RolledBackAtUtc");

        foreach (var task in tasks)
        {
            var execution = task.Executions
                .Where(e => e.Mode == RemediationMode.Execute && !e.IsRollback)
                .OrderByDescending(e => e.Id)
                .FirstOrDefault();

            var approvers = string.Join(" | ", task.ApprovalRequests
                .SelectMany(r => r.Signatures)
                .Where(s => s.Decision == ApprovalDecision.Approved)
                .Select(s => $"{s.Approver} ({s.ApproverRole}) {s.DecidedAt:u}"));

            csv.AppendLine(string.Join(',',
                Escape(task.ControlDefinition?.VendorReference),
                Escape(task.ControlDefinition?.Title),
                Escape(task.ControlDefinition?.Domain),
                Escape(task.ControlDefinition?.Severity.ToString()),
                Escape(task.Finding?.FailureReason?.ReplaceLineEndings(" ")),
                Escape(execution?.ExecutedBy ?? task.CreatedBy),
                Escape(execution?.CompletedAt?.ToString("u", CultureInfo.InvariantCulture)),
                Escape(task.Status.ToString()),
                Escape(approvers),
                Escape(task.RequiredSignatures.ToString()),
                Escape(task.VerificationOutcome?.ToString() ?? "NotVerified"),
                Escape(task.ConfigurationSnapshotId?.ToString()),
                Escape(task.RolledBackAt?.ToString("u", CultureInfo.InvariantCulture))));
        }

        var fileName = $"ag-one-safe-audit-{tenant.PrimaryDomain}-{DateTimeOffset.UtcNow:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n');
        var escaped = value.Replace("\"", "\"\"");

        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }
}

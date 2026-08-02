using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Audit;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Escalations;
using AgOneSafe.Domain.Remediation;
using AgOneSafe.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Application.Abstractions;

/// <summary>
/// Persistence surface the application services depend on. Keeps EF Core configuration in
/// Infrastructure while the orchestration logic stays testable against SQLite in memory.
/// </summary>
public interface IAgOneSafeDbContext
{
    DbSet<ControlDefinition> ControlDefinitions { get; }
    DbSet<ControlAction> ControlActions { get; }
    DbSet<AssertionRule> AssertionRules { get; }

    DbSet<TenantConnection> TenantConnections { get; }
    DbSet<ConnectorHealthCheck> ConnectorHealthChecks { get; }

    DbSet<Assessment> Assessments { get; }
    DbSet<ControlResult> ControlResults { get; }
    DbSet<Finding> Findings { get; }

    DbSet<RemediationTask> RemediationTasks { get; }
    DbSet<RemediationExecution> RemediationExecutions { get; }
    DbSet<RemediationBatch> RemediationBatches { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalSignature> ApprovalSignatures { get; }
    DbSet<ConfigurationSnapshot> ConfigurationSnapshots { get; }

    DbSet<ServiceEscalation> ServiceEscalations { get; }
    DbSet<AuditLogEntry> AuditLogEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

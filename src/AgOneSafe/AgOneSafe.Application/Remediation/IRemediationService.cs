using AgOneSafe.Domain;
using AgOneSafe.Domain.Remediation;

namespace AgOneSafe.Application.Remediation;

/// <summary>Identity of whoever asked for the action, recorded on every audit row.</summary>
public sealed record Actor(string Name, string Role)
{
    public static readonly Actor Agent = new("ag-one-agent", "Agent");
}

public interface IRemediationService
{
    /// <summary>Builds the plan, blast radius and script preview for a gap. Nothing is executed.</summary>
    Task<RemediationTask> PlanAsync(int findingId, Actor actor, int? batchId = null, CancellationToken cancellationToken = default);

    /// <summary>Rehearses the fix without writing to the tenant.</summary>
    Task<RemediationExecution> DryRunAsync(int taskId, Actor actor, CancellationToken cancellationToken = default);

    Task<ApprovalRequest> RequestApprovalAsync(int taskId, Actor actor, string justification, CancellationToken cancellationToken = default);

    Task<ApprovalRequest> RecordDecisionAsync(int taskId, Actor approver, ApprovalDecision decision, string? comment, CancellationToken cancellationToken = default);

    /// <summary>Snapshot, execute in tenant, then fire the targeted verification re-scan.</summary>
    Task<RemediationTask> ExecuteAsync(int taskId, Actor actor, CancellationToken cancellationToken = default);

    /// <summary>Restores the captured snapshot and re-verifies.</summary>
    Task<RemediationTask> RollbackAsync(int taskId, Actor actor, CancellationToken cancellationToken = default);

    Task<RemediationBatch> CreateBatchAsync(int tenantConnectionId, IReadOnlyCollection<int> findingIds, string name, DateTimeOffset? scheduledFor, Actor actor, CancellationToken cancellationToken = default);

    Task<int> ExecuteBatchAsync(int batchId, Actor actor, CancellationToken cancellationToken = default);

    /// <summary>Runs everything whose maintenance window has opened. Called by the scheduler.</summary>
    Task<int> ExecuteDueScheduledAsync(CancellationToken cancellationToken = default);

    Task AcceptRiskAsync(int findingId, Actor actor, string reason, CancellationToken cancellationToken = default);
}

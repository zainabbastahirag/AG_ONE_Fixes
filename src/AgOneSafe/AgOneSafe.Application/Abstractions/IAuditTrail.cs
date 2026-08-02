using AgOneSafe.Domain;
using AgOneSafe.Domain.Audit;

namespace AgOneSafe.Application.Abstractions;

public sealed class AuditEvent
{
    public required AuditEventType EventType { get; init; }
    public required string Summary { get; init; }

    public int? TenantConnectionId { get; init; }
    public string? ControlReference { get; init; }
    public int? FindingId { get; init; }
    public int? RemediationTaskId { get; init; }
    public int? AssessmentId { get; init; }

    public string Actor { get; init; } = "ag-one-agent";
    public string ActorRole { get; init; } = "Agent";

    public object? Details { get; init; }
}

/// <summary>Writes the hash-chained proof records used by the audit export and Proof Center.</summary>
public interface IAuditTrail
{
    Task<AuditLogEntry> RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>Walks the chain and reports the first entry whose hash does not line up.</summary>
    Task<(bool IsIntact, long? BrokenAtId)> VerifyChainAsync(CancellationToken cancellationToken = default);
}

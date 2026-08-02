using System.ComponentModel.DataAnnotations;

namespace AgOneSafe.Domain.Audit;

/// <summary>
/// Append-only proof record. Each row carries the SHA-256 of the previous row, so removing or
/// editing history breaks the chain and the Proof Center reports the log as tampered.
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }

    public int? TenantConnectionId { get; set; }

    public AuditEventType EventType { get; set; }

    [MaxLength(32)]
    public string? ControlReference { get; set; }

    public int? FindingId { get; set; }
    public int? RemediationTaskId { get; set; }
    public int? AssessmentId { get; set; }

    /// <summary>"ag-one-agent" for autonomous steps, otherwise the signed-in principal.</summary>
    [MaxLength(128)]
    public string Actor { get; set; } = "ag-one-agent";

    [MaxLength(64)]
    public string ActorRole { get; set; } = "Agent";

    [MaxLength(1024)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Structured payload: parameters, outcome, approver list, verification status.</summary>
    public string DetailsJson { get; set; } = "{}";

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(64)]
    public string PreviousHash { get; set; } = string.Empty;

    [MaxLength(64)]
    public string EntryHash { get; set; } = string.Empty;
}

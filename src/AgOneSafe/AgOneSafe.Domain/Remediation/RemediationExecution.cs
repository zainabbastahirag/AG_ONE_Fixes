using System.ComponentModel.DataAnnotations;

namespace AgOneSafe.Domain.Remediation;

/// <summary>One attempt to run a remediation payload - either a rehearsal or the real change.</summary>
public class RemediationExecution
{
    public int Id { get; set; }

    public int RemediationTaskId { get; set; }
    public RemediationTask? RemediationTask { get; set; }

    public RemediationMode Mode { get; set; }

    public ExecutorType ExecutorType { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>True when this run restored a snapshot rather than applying the fix.</summary>
    public bool IsRollback { get; set; }

    public string ExecutedCommand { get; set; } = string.Empty;

    public string Output { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Error { get; set; }

    public int DurationMs { get; set; }

    [MaxLength(128)]
    public string ExecutedBy { get; set; } = "ag-one-agent";

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

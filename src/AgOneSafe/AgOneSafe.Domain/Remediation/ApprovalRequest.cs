using System.ComponentModel.DataAnnotations;

namespace AgOneSafe.Domain.Remediation;

/// <summary>
/// Human-in-the-loop gate. Medium and High risk actions need one signature;
/// Critical-tier change needs two distinct approvers before the agent may execute.
/// </summary>
public class ApprovalRequest
{
    public int Id { get; set; }

    public int RemediationTaskId { get; set; }
    public RemediationTask? RemediationTask { get; set; }

    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;

    public int RequiredSignatures { get; set; } = 1;

    [MaxLength(2048)]
    public string Justification { get; set; } = string.Empty;

    [MaxLength(128)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);

    public List<ApprovalSignature> Signatures { get; set; } = new();

    public int ApprovedCount => Signatures.Count(s => s.Decision == ApprovalDecision.Approved);

    public bool IsSatisfied => Decision == ApprovalDecision.Approved && ApprovedCount >= RequiredSignatures;
}

public class ApprovalSignature
{
    public int Id { get; set; }

    public int ApprovalRequestId { get; set; }
    public ApprovalRequest? ApprovalRequest { get; set; }

    [MaxLength(128)]
    public string Approver { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ApproverRole { get; set; } = string.Empty;

    public ApprovalDecision Decision { get; set; }

    [MaxLength(1024)]
    public string? Comment { get; set; }

    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;
}

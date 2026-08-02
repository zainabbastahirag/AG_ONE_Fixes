using System.ComponentModel.DataAnnotations;
using AgOneSafe.Domain.Tenants;

namespace AgOneSafe.Domain.Assessments;

/// <summary>One baseline scan of a tenant against the enabled part of the control catalog.</summary>
public class Assessment
{
    public int Id { get; set; }

    public int TenantConnectionId { get; set; }
    public TenantConnection? TenantConnection { get; set; }

    public AssessmentStatus Status { get; set; } = AssessmentStatus.Queued;
    public AssessmentTrigger Trigger { get; set; } = AssessmentTrigger.Manual;

    /// <summary>Maturity target the scan was scored against.</summary>
    public ProfileLevel TargetLevel { get; set; } = ProfileLevel.L1;

    /// <summary>Restricts a verification re-scan to the controls that were just remediated.</summary>
    [MaxLength(1024)]
    public string? ScopedControlReferences { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public int TotalControls { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int ErrorCount { get; set; }
    public int ManualCount { get; set; }
    public int NotApplicableCount { get; set; }

    /// <summary>Weighted posture score, 0-100.</summary>
    public double PostureScore { get; set; }

    [MaxLength(64)]
    public string TriggeredBy { get; set; } = "system";

    [MaxLength(2048)]
    public string? FailureReason { get; set; }

    public List<ControlResult> Results { get; set; } = new();

    public TimeSpan? Duration => CompletedAt is null ? null : CompletedAt - StartedAt;

    public int ScoredCount => PassedCount + FailedCount;
}

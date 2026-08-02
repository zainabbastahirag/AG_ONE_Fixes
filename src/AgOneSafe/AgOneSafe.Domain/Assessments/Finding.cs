using System.ComponentModel.DataAnnotations;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Remediation;
using AgOneSafe.Domain.Tenants;

namespace AgOneSafe.Domain.Assessments;

/// <summary>
/// A failed control promoted into a trackable gap. Findings survive across assessments so the
/// Open -> Approved -> Remediated -> Verified lifecycle and its audit trail stay intact.
/// </summary>
public class Finding
{
    public int Id { get; set; }

    public int TenantConnectionId { get; set; }
    public TenantConnection? TenantConnection { get; set; }

    public int ControlDefinitionId { get; set; }
    public ControlDefinition? ControlDefinition { get; set; }

    /// <summary>Assessment that first raised the gap.</summary>
    public int FirstDetectedAssessmentId { get; set; }

    /// <summary>Most recent control evaluation backing this finding.</summary>
    public int? LatestControlResultId { get; set; }
    public ControlResult? LatestControlResult { get; set; }

    public FindingStatus Status { get; set; } = FindingStatus.Open;

    public Severity Severity { get; set; }

    [MaxLength(4000)]
    public string FailureReason { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string RequiredAction { get; set; } = string.Empty;

    /// <summary>Risk score (0-100) combining severity, CIS level and control risk weight.</summary>
    public double RiskScore { get; set; }

    public DateTimeOffset FirstDetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RemediatedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }

    [MaxLength(1024)]
    public string? RiskAcceptanceReason { get; set; }

    [MaxLength(128)]
    public string? RiskAcceptedBy { get; set; }

    public List<RemediationTask> RemediationTasks { get; set; } = new();

    public bool IsClosed => Status is FindingStatus.Verified or FindingStatus.RiskAccepted;
}

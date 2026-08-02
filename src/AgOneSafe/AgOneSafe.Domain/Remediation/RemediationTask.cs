using System.ComponentModel.DataAnnotations;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Tenants;

namespace AgOneSafe.Domain.Remediation;

/// <summary>
/// The unit of agentic work: one gap, one planned fix, one approval trail, one rollback point.
/// A task walks Draft -> PendingApproval -> Approved -> Running -> Succeeded -> (verified | rolled back).
/// </summary>
public class RemediationTask
{
    public int Id { get; set; }

    public int FindingId { get; set; }
    public Finding? Finding { get; set; }

    public int TenantConnectionId { get; set; }
    public TenantConnection? TenantConnection { get; set; }

    public int ControlDefinitionId { get; set; }
    public ControlDefinition? ControlDefinition { get; set; }

    /// <summary>Set when the task was created as part of a bulk selection.</summary>
    public int? RemediationBatchId { get; set; }
    public RemediationBatch? RemediationBatch { get; set; }

    public RemediationStatus Status { get; set; } = RemediationStatus.Draft;

    [MaxLength(2048)]
    public string PlanSummary { get; set; } = string.Empty;

    /// <summary>Exact PowerShell / Graph call the agent will run, shown before approval.</summary>
    public string ScriptPreview { get; set; } = string.Empty;

    /// <summary>Serialised <c>BlastRadius</c>: impacted users, resources, policies and warnings.</summary>
    public string BlastRadiusJson { get; set; } = "{}";

    public Severity ImpactSeverity { get; set; } = Severity.Low;

    /// <summary>Two signatures are demanded for Critical-tier change.</summary>
    public int RequiredSignatures { get; set; } = 1;

    public bool RequiresApproval { get; set; } = true;

    public DateTimeOffset? ScheduledFor { get; set; }

    [MaxLength(128)]
    public string CreatedBy { get; set; } = "system";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public int? ConfigurationSnapshotId { get; set; }
    public ConfigurationSnapshot? ConfigurationSnapshot { get; set; }

    /// <summary>Targeted re-scan fired automatically once the fix lands.</summary>
    public int? VerificationAssessmentId { get; set; }

    public ControlOutcome? VerificationOutcome { get; set; }

    [MaxLength(2048)]
    public string? VerificationMessage { get; set; }

    public DateTimeOffset? RolledBackAt { get; set; }

    [MaxLength(128)]
    public string? RolledBackBy { get; set; }

    public List<RemediationExecution> Executions { get; set; } = new();
    public List<ApprovalRequest> ApprovalRequests { get; set; } = new();

    public bool HasDryRun => Executions.Any(e => e.Mode == RemediationMode.DryRun);

    public bool CanRollback =>
        Status == RemediationStatus.Succeeded && ConfigurationSnapshotId is not null && RolledBackAt is null;
}

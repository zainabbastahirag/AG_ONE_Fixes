using System.ComponentModel.DataAnnotations;
using AgOneSafe.Domain.Tenants;

namespace AgOneSafe.Domain.Remediation;

/// <summary>Groups several tasks so a CISO can approve and execute a wave of fixes in one action.</summary>
public class RemediationBatch
{
    public int Id { get; set; }

    public int TenantConnectionId { get; set; }
    public TenantConnection? TenantConnection { get; set; }

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public RemediationStatus Status { get; set; } = RemediationStatus.Draft;

    /// <summary>Off-peak maintenance window requested for the wave.</summary>
    public DateTimeOffset? ScheduledFor { get; set; }

    [MaxLength(128)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public List<RemediationTask> Tasks { get; set; } = new();
}

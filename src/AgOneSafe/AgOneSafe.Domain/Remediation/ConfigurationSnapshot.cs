using System.ComponentModel.DataAnnotations;
using AgOneSafe.Domain.Tenants;

namespace AgOneSafe.Domain.Remediation;

/// <summary>
/// Pre-execution capture of the exact tenant settings a remediation is about to change.
/// Rollback replays <see cref="RollbackPayload"/> with these values, so restoring is a data
/// operation rather than a guess.
/// </summary>
public class ConfigurationSnapshot
{
    public int Id { get; set; }

    public int TenantConnectionId { get; set; }
    public TenantConnection? TenantConnection { get; set; }

    public int ControlDefinitionId { get; set; }

    [MaxLength(32)]
    public string ControlReference { get; set; } = string.Empty;

    /// <summary>The settings as they were, keyed by property name.</summary>
    public string StateJson { get; set; } = "{}";

    /// <summary>Executable payload that writes <see cref="StateJson"/> back to the tenant.</summary>
    public string RollbackPayload { get; set; } = string.Empty;

    public ExecutorType ExecutorType { get; set; }
    public ConnectorModule Module { get; set; }

    /// <summary>SHA-256 of <see cref="StateJson"/>, proving the snapshot was not edited after capture.</summary>
    [MaxLength(64)]
    public string StateHash { get; set; } = string.Empty;

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(128)]
    public string CapturedBy { get; set; } = "ag-one-agent";
}

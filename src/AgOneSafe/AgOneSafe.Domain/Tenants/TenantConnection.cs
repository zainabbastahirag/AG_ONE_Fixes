using System.ComponentModel.DataAnnotations;

namespace AgOneSafe.Domain.Tenants;

/// <summary>
/// A customer Microsoft 365 / Azure tenant that AG ONE Safe is authorised to scan and remediate.
/// Secrets never live here - only a reference to the Key Vault entry holding them.
/// </summary>
public class TenantConnection
{
    public int Id { get; set; }

    [MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Entra tenant GUID.</summary>
    [MaxLength(64)]
    public string TenantId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string PrimaryDomain { get; set; } = string.Empty;

    /// <summary>Multi-tenant app registration (service principal) client id used for delegation.</summary>
    [MaxLength(64)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Key Vault secret identifier. The value itself is never persisted in the product database.</summary>
    [MaxLength(256)]
    public string SecretReference { get; set; } = string.Empty;

    /// <summary>Azure subscriptions in scope for the Azure benchmark, comma separated.</summary>
    [MaxLength(1024)]
    public string AzureSubscriptionIds { get; set; } = string.Empty;

    /// <summary>Azure Lighthouse delegated resource management is required for cross-tenant Azure writes.</summary>
    public bool LighthouseEnabled { get; set; }

    [MaxLength(256)]
    public string? LighthouseDelegationId { get; set; }

    /// <summary>Tenant SKUs detected at onboarding; drives the license-gap engine.</summary>
    [MaxLength(512)]
    public string LicenseSkus { get; set; } = string.Empty;

    public TenantConnectionStatus Status { get; set; } = TenantConnectionStatus.NotConnected;

    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Simulation;

    /// <summary>Target maturity the customer signed up to. Controls above it are reported but not queued.</summary>
    public ProfileLevel TargetLevel { get; set; } = ProfileLevel.L1;

    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? LastHealthCheckAt { get; set; }
    public DateTimeOffset? LastAssessmentAt { get; set; }

    [MaxLength(1024)]
    public string? LastHealthMessage { get; set; }

    public double LatestPostureScore { get; set; }

    public List<ConnectorHealthCheck> HealthChecks { get; set; } = new();
}

/// <summary>Per-workload permission and reachability probe shown on the connection health screen.</summary>
public class ConnectorHealthCheck
{
    public int Id { get; set; }

    public int TenantConnectionId { get; set; }
    public TenantConnection? TenantConnection { get; set; }

    public ConnectorModule Module { get; set; }

    public bool IsHealthy { get; set; }

    [MaxLength(512)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Scopes the app registration actually holds for this workload.</summary>
    [MaxLength(1024)]
    public string GrantedScopes { get; set; } = string.Empty;

    /// <summary>Scopes required by the catalog but not consented yet.</summary>
    [MaxLength(1024)]
    public string MissingScopes { get; set; } = string.Empty;

    public int LatencyMs { get; set; }

    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}

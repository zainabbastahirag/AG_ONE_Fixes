using AgOne.Entitlements.Domain;

namespace AgOne.Entitlements.Services;

/// <summary>Builds and answers questions about a tenant's entitlement snapshot.</summary>
public interface IEntitlementService
{
    /// <summary>Compute (or fetch cached) the entitlement snapshot for a tenant + product.</summary>
    Task<EntitlementSnapshot> GetSnapshotAsync(Guid tenantId, ProductKey product, CancellationToken ct = default);

    /// <summary>Pure feature gate: is the feature included in the tenant's active plan?</summary>
    Task<bool> HasFeatureAsync(Guid tenantId, string featureKey, CancellationToken ct = default);
}

/// <summary>
/// The single decision point that combines the two orthogonal authorization layers:
/// RBAC (does the user's role grant it?) AND entitlement (does the plan include it?).
/// </summary>
public interface IAccessResolver
{
    /// <summary>
    /// True only when the user's role grants <paramref name="permissionCode"/> AND the tenant's
    /// subscription entitles it. This is the effective-permission rule for the whole platform.
    /// </summary>
    Task<AccessDecision> AuthorizePermissionAsync(
        Guid tenantId, Guid userId, string permissionCode, CancellationToken ct = default);

    /// <summary>The user's effective permissions = RBAC permissions ∩ plan-entitled permissions.</summary>
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        Guid tenantId, Guid userId, ProductKey product, CancellationToken ct = default);
}

/// <summary>Result of an authorization check, carrying the reason for denial for good UX/telemetry.</summary>
public sealed record AccessDecision(bool Allowed, AccessDenyReason Reason = AccessDenyReason.None)
{
    public static readonly AccessDecision Allow = new(true);
    public static AccessDecision Deny(AccessDenyReason reason) => new(false, reason);
}

public enum AccessDenyReason
{
    None = 0,
    /// <summary>Role does not grant the permission — a true RBAC "403".</summary>
    NotPermittedByRole = 1,
    /// <summary>Role grants it, but the plan does not include the feature — an upsell "402".</summary>
    NotIncludedInPlan = 2,
    /// <summary>Subscription is expired/canceled/past grace — a billing "402".</summary>
    SubscriptionInactive = 3
}

namespace AgOne.Entitlements.Domain;

/// <summary>
/// An immutable, cacheable projection of "what a tenant is entitled to for one product right now".
/// Computed once per (tenant, product) and cached; RBAC checks then intersect against it cheaply.
/// </summary>
public sealed class EntitlementSnapshot
{
    public required Guid TenantId { get; init; }
    public required ProductKey Product { get; init; }
    public required string PlanKey { get; init; }
    public required SubscriptionStatus Status { get; init; }

    /// <summary>True when the subscription currently grants access (active/trialing/in-grace).</summary>
    public required bool AccessGranted { get; init; }

    /// <summary>Human-readable reason when <see cref="AccessGranted"/> is false (expired, canceled...).</summary>
    public string? DenyReason { get; init; }

    /// <summary>Feature keys the tenant is entitled to (plan + add-ons, minus coming-soon).</summary>
    public required IReadOnlySet<string> EntitledFeatureKeys { get; init; }

    /// <summary>
    /// Permission codes unlocked by the entitled features. This is the set the RBAC
    /// permissions are intersected with to produce a user's effective permissions.
    /// </summary>
    public required IReadOnlySet<string> EntitledPermissionCodes { get; init; }

    /// <summary>An empty, access-denied snapshot (no subscription / hard denial).</summary>
    public static EntitlementSnapshot Denied(Guid tenantId, ProductKey product, string reason) => new()
    {
        TenantId = tenantId,
        Product = product,
        PlanKey = string.Empty,
        Status = SubscriptionStatus.Expired,
        AccessGranted = false,
        DenyReason = reason,
        EntitledFeatureKeys = new HashSet<string>(),
        EntitledPermissionCodes = new HashSet<string>()
    };

    public bool HasFeature(string featureKey) =>
        AccessGranted && EntitledFeatureKeys.Contains(featureKey);

    public bool EntitlesPermission(string permissionCode) =>
        AccessGranted && EntitledPermissionCodes.Contains(permissionCode);
}

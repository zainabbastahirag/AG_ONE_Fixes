using AgOne.Entitlements.Domain;

namespace AgOne.Entitlements.Gateway;

/// <summary>
/// Per-product slice of a user's effective access, returned by the AG ONE gateway.
/// This is what the Hire / Learn / Work apps cache and enforce — already intersected
/// with the tenant's subscription entitlement, so the apps need no entitlement logic.
/// </summary>
public sealed record ProductEntitlement(
    ProductKey Product,
    string PlanKey,
    SubscriptionStatus Status,
    bool AccessGranted,
    IReadOnlyCollection<string> EntitledFeatureKeys,
    IReadOnlyCollection<string> EffectivePermissionCodes);

/// <summary>
/// The payload AG ONE returns from <c>GET /api/permissions/{userId}</c>.
/// It carries the already-computed effective permissions (RBAC ∩ entitlement) plus a
/// <see cref="Version"/> the client apps compare to decide whether to refresh early,
/// and a <see cref="CacheTtlSeconds"/> (your existing 10-minute window).
/// </summary>
public sealed record EffectivePermissionResponse(
    Guid TenantId,
    Guid UserId,
    DateTimeOffset GeneratedAtUtc,
    int CacheTtlSeconds,
    string Version,
    IReadOnlyList<ProductEntitlement> Products,
    IReadOnlyCollection<string> AllPermissionCodes);

using AgOne.Entitlements.Abstractions;
using AgOne.Entitlements.Domain;

namespace AgOne.Entitlements.Services;

/// <summary>
/// Combines RBAC (existing) with entitlement (new). This is intentionally tiny: the whole
/// "less effort" story is that you keep your existing permission checks and just intersect
/// them with the plan-entitled permission set produced by the <see cref="IEntitlementService"/>.
/// </summary>
public sealed class AccessResolver : IAccessResolver
{
    private readonly IEntitlementService _entitlements;
    private readonly IUserPermissionProvider _userPermissions;
    private readonly ICatalogProvider _catalog;

    public AccessResolver(
        IEntitlementService entitlements,
        IUserPermissionProvider userPermissions,
        ICatalogProvider catalog)
    {
        _entitlements = entitlements;
        _userPermissions = userPermissions;
        _catalog = catalog;
    }

    public async Task<AccessDecision> AuthorizePermissionAsync(
        Guid tenantId, Guid userId, string permissionCode, CancellationToken ct = default)
    {
        var product = ResolveProduct(permissionCode);

        var rbac = await _userPermissions.GetPermissionCodesAsync(userId, ct);
        var hasByRole = rbac.Contains(permissionCode);

        var snapshot = await _entitlements.GetSnapshotAsync(tenantId, product, ct);

        // Order of reasons matters for good UX:
        //  - not entitled by plan/billing -> upsell/renew (402)
        //  - not granted by role          -> access denied (403)
        if (!snapshot.AccessGranted)
            return AccessDecision.Deny(AccessDenyReason.SubscriptionInactive);

        if (!hasByRole)
            return AccessDecision.Deny(AccessDenyReason.NotPermittedByRole);

        if (!snapshot.EntitlesPermission(permissionCode))
            return AccessDecision.Deny(AccessDenyReason.NotIncludedInPlan);

        return AccessDecision.Allow;
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        Guid tenantId, Guid userId, ProductKey product, CancellationToken ct = default)
    {
        var rbac = await _userPermissions.GetPermissionCodesAsync(userId, ct);
        var snapshot = await _entitlements.GetSnapshotAsync(tenantId, product, ct);

        if (!snapshot.AccessGranted)
            return new HashSet<string>();

        // Effective = RBAC ∩ entitled.
        var effective = new HashSet<string>(rbac, StringComparer.OrdinalIgnoreCase);
        effective.IntersectWith(snapshot.EntitledPermissionCodes);
        return effective;
    }

    /// <summary>
    /// Resolve the owning product from a permission code. Convention: codes are namespaced
    /// as "&lt;product&gt;.&lt;resource&gt;.&lt;action&gt;" (e.g. "hire.employee.read"). Anything not
    /// matching a product prefix is treated as a cross-cutting Platform permission.
    /// </summary>
    private ProductKey ResolveProduct(string permissionCode)
    {
        var prefix = permissionCode.Split('.', 2)[0];
        return prefix.ToLowerInvariant() switch
        {
            "hire" => ProductKey.Hire,
            "learn" => ProductKey.Learn,
            "work" => ProductKey.Work,
            _ => ProductKey.Platform
        };
    }
}

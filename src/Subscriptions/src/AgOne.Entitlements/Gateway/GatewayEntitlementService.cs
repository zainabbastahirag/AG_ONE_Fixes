using AgOne.Entitlements.Domain;
using AgOne.Entitlements.Services;

namespace AgOne.Entitlements.Gateway;

/// <summary>
/// Runs inside the AG ONE gateway. Turns the existing "permissions by user id" call into an
/// "effective permissions by user id" call by intersecting the user's RBAC permissions with
/// what their tenant's subscription actually entitles — per product, in one round-trip.
/// The Hire/Learn/Work apps keep caching and enforcing exactly what they receive.
/// </summary>
public interface IGatewayEntitlementService
{
    Task<EffectivePermissionResponse> BuildAsync(
        Guid tenantId, Guid userId, IEnumerable<ProductKey> subscribedProducts,
        int cacheTtlSeconds = 600, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class GatewayEntitlementService : IGatewayEntitlementService
{
    private readonly IAccessResolver _access;
    private readonly IEntitlementService _entitlements;

    public GatewayEntitlementService(IAccessResolver access, IEntitlementService entitlements)
    {
        _access = access;
        _entitlements = entitlements;
    }

    public async Task<EffectivePermissionResponse> BuildAsync(
        Guid tenantId, Guid userId, IEnumerable<ProductKey> subscribedProducts,
        int cacheTtlSeconds = 600, CancellationToken ct = default)
    {
        var products = new List<ProductEntitlement>();
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in subscribedProducts.Distinct())
        {
            var snapshot = await _entitlements.GetSnapshotAsync(tenantId, product, ct);
            var effective = await _access.GetEffectivePermissionsAsync(tenantId, userId, product, ct);

            products.Add(new ProductEntitlement(
                product,
                snapshot.PlanKey,
                snapshot.Status,
                snapshot.AccessGranted,
                snapshot.EntitledFeatureKeys.ToArray(),
                effective.ToArray()));

            foreach (var code in effective) all.Add(code);
        }

        // Version lets client apps bust their 10-min cache early when entitlement changes.
        // Derived from the entitlement inputs so it only changes when access actually changes.
        var version = ComputeVersion(tenantId, userId, products);

        return new EffectivePermissionResponse(
            TenantId: tenantId,
            UserId: userId,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            CacheTtlSeconds: cacheTtlSeconds,
            Version: version,
            Products: products,
            AllPermissionCodes: all);
    }

    private static string ComputeVersion(Guid tenantId, Guid userId, IReadOnlyList<ProductEntitlement> products)
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + tenantId.GetHashCode();
            hash = hash * 31 + userId.GetHashCode();
            foreach (var p in products.OrderBy(p => p.Product))
            {
                hash = hash * 31 + (int)p.Product;
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(p.PlanKey);
                hash = hash * 31 + p.Status.GetHashCode();
                foreach (var code in p.EffectivePermissionCodes.OrderBy(c => c, StringComparer.Ordinal))
                    hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(code);
            }
            return hash.ToString("x8");
        }
    }
}

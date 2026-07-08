using AgOne.Entitlements.Domain;
using AgOne.Entitlements.Services;
using Microsoft.Extensions.Caching.Memory;

namespace AgOne.Entitlements.AspNetCore;

/// <summary>
/// Caches entitlement snapshots per (tenant, product) so the hot path (every RBAC check)
/// avoids re-reading the subscription + re-expanding the catalog. Invalidate on subscription
/// change (upgrade/downgrade/cancel/renew) via <see cref="Invalidate"/> — wire it into your
/// billing webhook handler.
/// </summary>
public sealed class CachingEntitlementService : IEntitlementService
{
    private readonly EntitlementService _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public CachingEntitlementService(EntitlementService inner, IMemoryCache cache, TimeSpan? ttl = null)
    {
        _inner = inner;
        _cache = cache;
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
    }

    public Task<EntitlementSnapshot> GetSnapshotAsync(Guid tenantId, ProductKey product, CancellationToken ct = default)
        => _cache.GetOrCreateAsync(Key(tenantId, product), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _ttl;
            return _inner.GetSnapshotAsync(tenantId, product, ct);
        })!;

    public Task<bool> HasFeatureAsync(Guid tenantId, string featureKey, CancellationToken ct = default)
        => _inner.HasFeatureAsync(tenantId, featureKey, ct);

    public void Invalidate(Guid tenantId, ProductKey product) => _cache.Remove(Key(tenantId, product));

    private static string Key(Guid tenantId, ProductKey product) => $"ent:{tenantId:N}:{(int)product}";
}

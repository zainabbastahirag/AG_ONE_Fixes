using AgOne.Entitlements.Abstractions;
using AgOne.Entitlements.Domain;

namespace AgOne.Entitlements.Tests;

/// <summary>Deterministic clock for grace/expiry tests.</summary>
public sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }
    public FixedClock(DateTimeOffset now) => UtcNow = now;
}

/// <summary>In-memory subscription store keyed by (tenant, product).</summary>
public sealed class FakeSubscriptionStore : ISubscriptionStore
{
    private readonly Dictionary<(Guid, ProductKey), Subscription> _subs = new();

    public FakeSubscriptionStore Add(Subscription sub)
    {
        _subs[(sub.TenantId, sub.Product)] = sub;
        return this;
    }

    public Task<Subscription?> GetAsync(Guid tenantId, ProductKey product, CancellationToken ct = default)
        => Task.FromResult(_subs.TryGetValue((tenantId, product), out var s) ? s : null);
}

/// <summary>In-memory RBAC provider: the permissions a user's roles already grant today.</summary>
public sealed class FakeUserPermissionProvider : IUserPermissionProvider
{
    private readonly Dictionary<Guid, HashSet<string>> _perms = new();

    public FakeUserPermissionProvider Grant(Guid userId, params string[] codes)
    {
        if (!_perms.TryGetValue(userId, out var set))
            _perms[userId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in codes) set.Add(c);
        return this;
    }

    public Task<IReadOnlySet<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<string>>(
            _perms.TryGetValue(userId, out var s) ? s : new HashSet<string>());
}

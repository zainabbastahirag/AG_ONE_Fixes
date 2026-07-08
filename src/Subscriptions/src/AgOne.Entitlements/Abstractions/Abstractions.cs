using AgOne.Entitlements.Domain;

namespace AgOne.Entitlements.Abstractions;

/// <summary>Abstracts the clock so grace/expiry logic is testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Default wall-clock implementation.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Read model of the plan/feature catalog. Backed either by the in-memory
/// <c>PlanCatalog</c> (source of truth in code) or by the billing.* tables.
/// </summary>
public interface ICatalogProvider
{
    IReadOnlyCollection<Feature> Features { get; }
    IReadOnlyCollection<Plan> Plans { get; }
    Feature? FindFeature(string featureKey);
    Plan? FindPlan(string planKey);
}

/// <summary>Loads the current subscription for a tenant + product.</summary>
public interface ISubscriptionStore
{
    Task<Subscription?> GetAsync(Guid tenantId, ProductKey product, CancellationToken ct = default);
}

/// <summary>
/// Supplies the RBAC permission codes a user already has (union across their roles),
/// exactly as computed today from core.UserRoles + core.RolePermissions + core.Permissions.
/// The entitlement layer never replaces this; it only intersects with it.
/// </summary>
public interface IUserPermissionProvider
{
    Task<IReadOnlySet<string>> GetPermissionCodesAsync(Guid userId, CancellationToken ct = default);
}

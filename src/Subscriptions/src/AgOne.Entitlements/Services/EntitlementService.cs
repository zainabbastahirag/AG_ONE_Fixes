using AgOne.Entitlements.Abstractions;
using AgOne.Entitlements.Domain;

namespace AgOne.Entitlements.Services;

/// <summary>
/// Default entitlement engine. Turns a tenant's <see cref="Subscription"/> + the catalog
/// into an <see cref="EntitlementSnapshot"/> by:
/// 1. deciding whether the subscription currently grants access (status + grace/expiry), then
/// 2. expanding plan features (+ add-ons) into feature keys and their permission codes.
/// </summary>
public sealed class EntitlementService : IEntitlementService
{
    private readonly ISubscriptionStore _subscriptions;
    private readonly ICatalogProvider _catalog;
    private readonly IClock _clock;

    public EntitlementService(ISubscriptionStore subscriptions, ICatalogProvider catalog, IClock clock)
    {
        _subscriptions = subscriptions;
        _catalog = catalog;
        _clock = clock;
    }

    public async Task<EntitlementSnapshot> GetSnapshotAsync(
        Guid tenantId, ProductKey product, CancellationToken ct = default)
    {
        var sub = await _subscriptions.GetAsync(tenantId, product, ct);
        if (sub is null)
            return EntitlementSnapshot.Denied(tenantId, product, "No subscription for this product.");

        var (granted, denyReason) = EvaluateAccess(sub);

        var plan = _catalog.FindPlan(sub.PlanKey);
        var featureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (plan is not null)
            foreach (var fk in plan.FeatureKeys) featureKeys.Add(fk);
        foreach (var fk in sub.AddOnFeatureKeys) featureKeys.Add(fk);

        // Expand features -> permission codes, dropping coming-soon features entirely.
        var permissionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var liveFeatureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in featureKeys)
        {
            var feature = _catalog.FindFeature(fk);
            if (feature is null || feature.IsComingSoon) continue;
            liveFeatureKeys.Add(feature.Key);
            foreach (var code in feature.PermissionCodes) permissionCodes.Add(code);
        }

        return new EntitlementSnapshot
        {
            TenantId = tenantId,
            Product = product,
            PlanKey = sub.PlanKey,
            Status = sub.Status,
            AccessGranted = granted,
            DenyReason = denyReason,
            EntitledFeatureKeys = liveFeatureKeys,
            EntitledPermissionCodes = permissionCodes
        };
    }

    public async Task<bool> HasFeatureAsync(Guid tenantId, string featureKey, CancellationToken ct = default)
    {
        var feature = _catalog.FindFeature(featureKey);
        if (feature is null) return false;
        var snapshot = await GetSnapshotAsync(tenantId, feature.Product, ct);
        return snapshot.HasFeature(featureKey);
    }

    /// <summary>
    /// Access rule by status:
    /// - Trialing: granted until TrialEnd.
    /// - Active: granted until CurrentPeriodEnd (null = open-ended).
    /// - PastDue: granted until CurrentPeriodEnd + GracePeriod, then cut.
    /// - Canceled/Expired: never granted.
    /// </summary>
    private (bool granted, string? reason) EvaluateAccess(Subscription sub)
    {
        var now = _clock.UtcNow;
        switch (sub.Status)
        {
            case SubscriptionStatus.Trialing:
                return sub.TrialEndUtc is { } t && now > t
                    ? (false, "Trial expired.")
                    : (true, null);

            case SubscriptionStatus.Active:
                return sub.CurrentPeriodEndUtc is { } end && now > end
                    ? (false, "Subscription period ended.")
                    : (true, null);

            case SubscriptionStatus.PastDue:
                var graceEnd = (sub.CurrentPeriodEndUtc ?? now) + sub.GracePeriod;
                return now > graceEnd
                    ? (false, "Payment past due and grace period elapsed.")
                    : (true, null);

            case SubscriptionStatus.Canceled:
                return (false, "Subscription canceled.");

            case SubscriptionStatus.Expired:
            default:
                return (false, "Subscription expired.");
        }
    }
}

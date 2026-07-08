namespace AgOne.Entitlements.Domain;

/// <summary>
/// A customer organization. In the multi-tenant model every row of the business tables
/// (hire.*, learn.*, work.*) carries a TenantId; a tenant subscribes independently to each product.
/// </summary>
public sealed class Tenant
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// A tenant's subscription to one product. The engine reads this to decide which features
/// (and therefore which permissions) are entitled for that tenant/product pair.
/// </summary>
public sealed class Subscription
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required ProductKey Product { get; init; }

    /// <summary>The plan key currently subscribed to (e.g. "hire.standard").</summary>
    public required string PlanKey { get; init; }

    public required SubscriptionStatus Status { get; init; }

    public DateTimeOffset StartUtc { get; init; }

    /// <summary>End of the current paid period; used for expiry evaluation.</summary>
    public DateTimeOffset? CurrentPeriodEndUtc { get; init; }

    /// <summary>End of trial (when <see cref="Status"/> is Trialing).</summary>
    public DateTimeOffset? TrialEndUtc { get; init; }

    /// <summary>
    /// How long a PastDue subscription keeps working after a failed payment before access is cut.
    /// </summary>
    public TimeSpan GracePeriod { get; init; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Extra feature keys sold on top of the plan (add-ons). Entitled exactly like plan features.
    /// </summary>
    public IReadOnlyCollection<string> AddOnFeatureKeys { get; init; } = Array.Empty<string>();
}

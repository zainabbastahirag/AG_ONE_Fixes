namespace AgOne.Entitlements.Domain;

/// <summary>
/// The three products of the suite. Each product maps 1:1 to a database schema
/// (hire.*, learn.*, work.*). <see cref="Platform"/> is a cross-cutting pseudo-product
/// used for features that are sold across every product (API, SSO, compliance, AI...).
/// </summary>
public enum ProductKey
{
    Hire = 1,
    Learn = 2,
    Work = 3,
    Platform = 99
}

/// <summary>
/// Commercial plan tiers. Ordered so a higher tier can be compared as "&gt;=" a lower one.
/// A subscription always references a concrete <c>Plan</c> row; the tier is metadata used
/// for cumulative catalog authoring and upgrade/downgrade comparisons.
/// </summary>
public enum PlanTier
{
    Starter = 10,
    Lite = 20,
    Standard = 30,
    Enterprise = 40
}

/// <summary>
/// Lifecycle status of a subscription. Only <see cref="Active"/>, <see cref="Trialing"/>
/// and <see cref="PastDue"/> (while inside the grace window) grant access.
/// </summary>
public enum SubscriptionStatus
{
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4,
    Expired = 5
}

namespace AgOne.Entitlements.Domain;

/// <summary>
/// A sellable capability of a product (e.g. "Document Management", "Leave Request Workflow").
/// A feature is the unit that plans are composed of and that the UI toggles on/off.
/// <para>
/// The crucial bridge to the *existing* RBAC system is <see cref="PermissionCodes"/>:
/// each feature declares which already-existing <c>core.Permissions</c> codes it unlocks.
/// This is what lets subscription plans reuse the permissions you already wired up.
/// </para>
/// </summary>
public sealed class Feature
{
    public required string Key { get; init; }
    public required ProductKey Product { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>Features flagged "Coming Soon" are catalog-visible but never entitle access.</summary>
    public bool IsComingSoon { get; init; }

    /// <summary>
    /// The existing permission codes (rows in <c>core.Permissions</c>) that this feature gates.
    /// A user can only exercise one of these permissions when BOTH their role grants it AND
    /// their tenant's plan includes a feature that lists it here.
    /// </summary>
    public IReadOnlyCollection<string> PermissionCodes { get; init; } = Array.Empty<string>();
}

/// <summary>A commercial plan for a single product, e.g. hire/Standard.</summary>
public sealed class Plan
{
    public required string Key { get; init; }
    public required ProductKey Product { get; init; }
    public required PlanTier Tier { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>Feature keys bundled into this plan.</summary>
    public IReadOnlyCollection<string> FeatureKeys { get; init; } = Array.Empty<string>();
}

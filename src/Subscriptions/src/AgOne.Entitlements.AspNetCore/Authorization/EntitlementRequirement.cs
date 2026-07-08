using AgOne.Entitlements.Abstractions;
using AgOne.Entitlements.Services;
using Microsoft.AspNetCore.Authorization;

namespace AgOne.Entitlements.AspNetCore.Authorization;

/// <summary>Requirement carrying either a permission code or a feature key to check.</summary>
public sealed class EntitlementRequirement : IAuthorizationRequirement
{
    public string? PermissionCode { get; }
    public string? FeatureKey { get; }

    private EntitlementRequirement(string? permission, string? feature)
    {
        PermissionCode = permission;
        FeatureKey = feature;
    }

    public static EntitlementRequirement ForPermission(string code) => new(code, null);
    public static EntitlementRequirement ForFeature(string key) => new(null, key);
}

/// <summary>
/// The bridge between ASP.NET Core authorization and the entitlement engine. For permission
/// requirements it delegates to <see cref="IAccessResolver"/> (RBAC ∩ entitlement); for feature
/// requirements it asks the <see cref="IEntitlementService"/>.
/// </summary>
public sealed class EntitlementAuthorizationHandler : AuthorizationHandler<EntitlementRequirement>
{
    private readonly IAccessResolver _access;
    private readonly IEntitlementService _entitlements;
    private readonly ICatalogProvider _catalog;
    private readonly ITenantContext _tenant;

    public EntitlementAuthorizationHandler(
        IAccessResolver access,
        IEntitlementService entitlements,
        ICatalogProvider catalog,
        ITenantContext tenant)
    {
        _access = access;
        _entitlements = entitlements;
        _catalog = catalog;
        _tenant = tenant;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, EntitlementRequirement requirement)
    {
        if (!_tenant.IsResolved)
            return; // Unauthenticated / no tenant -> leave unsatisfied (401/403).

        if (requirement.PermissionCode is { } code)
        {
            var decision = await _access.AuthorizePermissionAsync(_tenant.TenantId, _tenant.UserId, code);
            if (decision.Allowed)
                context.Succeed(requirement);
            else
                context.Fail(new AuthorizationFailureReason(this, decision.Reason.ToString()));
            return;
        }

        if (requirement.FeatureKey is { } key)
        {
            var granted = await _entitlements.HasFeatureAsync(_tenant.TenantId, key);
            if (granted)
                context.Succeed(requirement);
            else
                context.Fail(new AuthorizationFailureReason(this, AccessDenyReason.NotIncludedInPlan.ToString()));
        }
    }
}

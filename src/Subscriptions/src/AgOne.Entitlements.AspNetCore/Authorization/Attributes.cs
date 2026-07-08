using Microsoft.AspNetCore.Authorization;

namespace AgOne.Entitlements.AspNetCore.Authorization;

/// <summary>
/// Guards an action/controller with a single permission code, enforcing BOTH the existing
/// RBAC grant AND the subscription entitlement. Drop-in replacement for a role-only attribute:
/// <c>[RequirePermission("hire.employee.write")]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public RequirePermissionAttribute(string permissionCode) => Policy = PolicyPrefix + permissionCode;
}

/// <summary>
/// Pure feature gate (no RBAC dependency). Use to hide/deny whole modules that are not in the
/// tenant's plan: <c>[RequireFeature(FeatureKeys.DocumentManagement)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireFeatureAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "feat:";

    public RequireFeatureAttribute(string featureKey) => Policy = PolicyPrefix + featureKey;
}

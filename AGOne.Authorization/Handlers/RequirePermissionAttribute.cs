using Microsoft.AspNetCore.Authorization;

namespace AGOne.Authorization.Handlers;

/// <summary>
/// Apply to controllers or actions to require specific permissions.
/// 
/// Usage:
///   [RequirePermission(Permissions.Work.Employee.Create)]
///   [RequirePermission(Permissions.Work.Employee.Read, Permissions.Work.Employee.Update, RequireAll = true)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "AGOnePermission:";

    public RequirePermissionAttribute(params string[] permissions)
    {
        Policy = PolicyPrefix + string.Join(",", permissions);
    }

    public RequirePermissionAttribute(string permission)
    {
        Policy = PolicyPrefix + permission;
    }
}

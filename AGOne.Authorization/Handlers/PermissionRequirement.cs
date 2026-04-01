using Microsoft.AspNetCore.Authorization;

namespace AGOne.Authorization.Handlers;

/// <summary>
/// ASP.NET Core authorization requirement that checks one or more permission codes.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string[] PermissionCodes { get; }
    public bool RequireAll { get; }

    public PermissionRequirement(string[] permissionCodes, bool requireAll = false)
    {
        PermissionCodes = permissionCodes;
        RequireAll = requireAll;
    }
}

public sealed class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Services.IPermissionService _permissionService;

    public PermissionRequirementHandler(Services.IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated == true)
            return;

        bool granted = requirement.RequireAll
            ? await _permissionService.HasAllPermissionsAsync(requirement.PermissionCodes)
            : await _permissionService.HasAnyPermissionAsync(requirement.PermissionCodes);

        if (granted)
            context.Succeed(requirement);
    }
}

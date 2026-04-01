using AGOne.Authorization.Services;

namespace AGOne.Authorization.Extensions;

/// <summary>
/// Convenience extension methods for use in Blazor components or service classes.
/// </summary>
public static class PermissionServiceExtensions
{
    public static async Task<bool> CanCreateAsync(this IPermissionService svc, string resourcePrefix)
        => await svc.HasPermissionAsync($"{resourcePrefix}.create");

    public static async Task<bool> CanReadAsync(this IPermissionService svc, string resourcePrefix)
        => await svc.HasPermissionAsync($"{resourcePrefix}.read");

    public static async Task<bool> CanUpdateAsync(this IPermissionService svc, string resourcePrefix)
        => await svc.HasPermissionAsync($"{resourcePrefix}.update");

    public static async Task<bool> CanDeleteAsync(this IPermissionService svc, string resourcePrefix)
        => await svc.HasPermissionAsync($"{resourcePrefix}.delete");
}

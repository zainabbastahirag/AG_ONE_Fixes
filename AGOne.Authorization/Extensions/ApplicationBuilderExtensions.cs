using AGOne.Authorization.Middleware;
using Microsoft.AspNetCore.Builder;

namespace AGOne.Authorization.Extensions;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Add permission refresh middleware for downstream products.
    /// Place after UseAuthentication() and UseAuthorization().
    /// 
    /// This middleware detects when AG ONE has bumped the permission version
    /// (after a role/permission change) and refreshes the local cache.
    /// </summary>
    public static IApplicationBuilder UseAGOnePermissionRefresh(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PermissionRefreshMiddleware>();
    }
}

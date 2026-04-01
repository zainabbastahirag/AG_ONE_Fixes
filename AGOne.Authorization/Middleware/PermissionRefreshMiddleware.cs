using AGOne.Authorization.Constants;
using AGOne.Authorization.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AGOne.Authorization.Middleware;

/// <summary>
/// Middleware for downstream apps. On each request, it compares the JWT's perm_version
/// against the cached version. If the JWT version is newer (AG ONE bumped it after a
/// role/permission change), the cache is invalidated so the next permission check
/// fetches fresh data from the gateway.
///
/// This is how newly-added permissions propagate to Work/Learn/Safe without re-login:
/// 1. Admin changes a role in AG ONE → gateway bumps UserPermissionVersion
/// 2. Next time the user's token is refreshed (or a new token is issued), it contains the new perm_version
/// 3. This middleware sees the new version → invalidates cache → fresh permissions loaded
/// </summary>
public sealed class PermissionRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PermissionRefreshMiddleware> _logger;

    public PermissionRefreshMiddleware(RequestDelegate next, ILogger<PermissionRefreshMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IPermissionService permissionService, ICurrentUserAccessor currentUser)
    {
        if (currentUser.IsAuthenticated && currentUser.UserId.HasValue && currentUser.TenantId.HasValue)
        {
            var jwtVersion = currentUser.PermissionVersion;
            if (jwtVersion.HasValue)
            {
                try
                {
                    var cached = await permissionService.GetUserPermissionsAsync(
                        currentUser.UserId.Value, currentUser.TenantId.Value);

                    if (jwtVersion.Value > cached.Version)
                    {
                        _logger.LogInformation(
                            "Permission version mismatch: JWT={JwtVer}, Cached={CachedVer}. Invalidating cache for user {UserId}",
                            jwtVersion.Value, cached.Version, currentUser.UserId.Value);

                        await permissionService.InvalidateUserPermissionsAsync(
                            currentUser.UserId.Value, currentUser.TenantId.Value);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Permission refresh check failed, continuing with existing permissions");
                }
            }
        }

        await _next(context);
    }
}

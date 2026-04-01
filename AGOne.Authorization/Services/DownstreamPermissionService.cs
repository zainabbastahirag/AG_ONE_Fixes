using System.Net.Http.Json;
using AGOne.Authorization.Constants;
using AGOne.Authorization.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AGOne.Authorization.Services;

/// <summary>
/// Permission service for downstream products (Work, Learn, Safe, Pulse, Spot).
/// Reads permissions from the JWT token claims. If the JWT perm_version is stale
/// (i.e. AG ONE bumped the version after a role change), it calls the gateway
/// API to fetch fresh permissions and updates the local cache.
/// </summary>
public sealed class DownstreamPermissionService : IPermissionService
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AGOneAuthOptions _options;
    private readonly ILogger<DownstreamPermissionService> _logger;

    public DownstreamPermissionService(
        ICurrentUserAccessor currentUser,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        AGOneAuthOptions options,
        ILogger<DownstreamPermissionService> logger)
    {
        _currentUser = currentUser;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(string permissionCode)
    {
        var perms = await GetCurrentUserPermissionsAsync();
        return perms.Permissions.Contains(permissionCode);
    }

    public async Task<bool> HasAllPermissionsAsync(params string[] permissionCodes)
    {
        var perms = await GetCurrentUserPermissionsAsync();
        return permissionCodes.All(p => perms.Permissions.Contains(p));
    }

    public async Task<bool> HasAnyPermissionAsync(params string[] permissionCodes)
    {
        var perms = await GetCurrentUserPermissionsAsync();
        return permissionCodes.Any(p => perms.Permissions.Contains(p));
    }

    public async Task<UserPermissionSet> GetCurrentUserPermissionsAsync()
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("No user context");
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("No tenant context");
        return await GetUserPermissionsAsync(userId, tenantId);
    }

    public async Task<UserPermissionSet> GetUserPermissionsAsync(Guid userId, Guid tenantId)
    {
        var cacheKey = $"perms:{tenantId}:{userId}";

        if (_cache.TryGetValue(cacheKey, out UserPermissionSet? cached) && cached != null)
        {
            var jwtVersion = _currentUser.PermissionVersion;
            if (jwtVersion == null || jwtVersion <= cached.Version)
                return cached;

            _logger.LogInformation(
                "JWT perm_version {JwtVer} > cached {CachedVer} for user {UserId}, refreshing from gateway",
                jwtVersion, cached.Version, userId);
        }

        var jwtPerms = _currentUser.JwtPermissions;
        var jwtPermVersion = _currentUser.PermissionVersion ?? 0;

        if (jwtPerms.Count > 0)
        {
            var fromJwt = new UserPermissionSet
            {
                UserId = userId,
                TenantId = tenantId,
                Version = jwtPermVersion,
                Permissions = new HashSet<string>(jwtPerms, StringComparer.OrdinalIgnoreCase),
                LoadedAt = DateTime.UtcNow
            };

            _cache.Set(cacheKey, fromJwt, _options.CacheDuration);

            _ = Task.Run(async () => await TryRefreshFromGatewayAsync(userId, tenantId, cacheKey, jwtPermVersion));

            return fromJwt;
        }

        return await RefreshFromGatewayAsync(userId, tenantId, cacheKey);
    }

    public Task InvalidateUserPermissionsAsync(Guid userId, Guid tenantId)
    {
        var cacheKey = $"perms:{tenantId}:{userId}";
        _cache.Remove(cacheKey);
        return Task.CompletedTask;
    }

    private async Task TryRefreshFromGatewayAsync(Guid userId, Guid tenantId, string cacheKey, long currentVersion)
    {
        try
        {
            var fresh = await FetchPermissionsFromGatewayAsync(userId, tenantId);
            if (fresh != null && fresh.Version > currentVersion)
            {
                _cache.Set(cacheKey, fresh, _options.CacheDuration);
                _logger.LogInformation("Background refresh: updated permissions for user {UserId} to version {Version}", userId, fresh.Version);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background permission refresh failed for user {UserId}", userId);
        }
    }

    private async Task<UserPermissionSet> RefreshFromGatewayAsync(Guid userId, Guid tenantId, string cacheKey)
    {
        var fresh = await FetchPermissionsFromGatewayAsync(userId, tenantId);
        if (fresh != null)
        {
            _cache.Set(cacheKey, fresh, _options.CacheDuration);
            return fresh;
        }

        return new UserPermissionSet
        {
            UserId = userId,
            TenantId = tenantId,
            Version = 0,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private async Task<UserPermissionSet?> FetchPermissionsFromGatewayAsync(Guid userId, Guid tenantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AGOneGateway");
            var response = await client.GetAsync($"api/internal/permissions/{tenantId}/{userId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gateway returned {Status} for permissions fetch", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UserPermissionSet>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch permissions from gateway for user {UserId}", userId);
            return null;
        }
    }
}

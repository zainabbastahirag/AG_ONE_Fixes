using AGOne.Authorization.Entities;
using AGOne.Authorization.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AGOne.Authorization.Services;

/// <summary>
/// Permission service for the AG ONE gateway — reads from EF Core database.
/// This is the source of truth. It populates JWT claims at token issuance time,
/// and bumps the version counter when permissions change.
/// </summary>
public sealed class GatewayPermissionService : IPermissionService
{
    private readonly AuthorizationDbContext _db;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMemoryCache _cache;
    private readonly AGOneAuthOptions _options;
    private readonly ILogger<GatewayPermissionService> _logger;

    public GatewayPermissionService(
        AuthorizationDbContext db,
        ICurrentUserAccessor currentUser,
        IMemoryCache cache,
        AGOneAuthOptions options,
        ILogger<GatewayPermissionService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
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
            return cached;

        var permissionCodes = await _db.UserRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        var version = await _db.UserPermissionVersions
            .Where(v => v.UserId == userId && v.TenantId == tenantId)
            .Select(v => v.Version)
            .FirstOrDefaultAsync();

        var result = new UserPermissionSet
        {
            UserId = userId,
            TenantId = tenantId,
            Version = version,
            Permissions = new HashSet<string>(permissionCodes, StringComparer.OrdinalIgnoreCase),
            LoadedAt = DateTime.UtcNow
        };

        _cache.Set(cacheKey, result, _options.CacheDuration);
        return result;
    }

    public async Task InvalidateUserPermissionsAsync(Guid userId, Guid tenantId)
    {
        var cacheKey = $"perms:{tenantId}:{userId}";
        _cache.Remove(cacheKey);

        var versionEntity = await _db.UserPermissionVersions
            .FirstOrDefaultAsync(v => v.UserId == userId && v.TenantId == tenantId);

        if (versionEntity != null)
        {
            versionEntity.Version++;
            versionEntity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.UserPermissionVersions.Add(new UserPermissionVersionEntity
            {
                UserId = userId,
                TenantId = tenantId,
                Version = 1,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Permission version bumped for user {UserId} tenant {TenantId}", userId, tenantId);
    }
}

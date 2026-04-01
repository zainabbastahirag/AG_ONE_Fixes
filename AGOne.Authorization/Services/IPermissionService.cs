using AGOne.Authorization.Models;

namespace AGOne.Authorization.Services;

/// <summary>
/// Core permission service — inject this in any product to check permissions.
/// Works in both AG ONE gateway (DB-backed) and downstream apps (JWT + cache + HTTP refresh).
/// </summary>
public interface IPermissionService
{
    /// <summary>Check if the current user has a specific permission.</summary>
    Task<bool> HasPermissionAsync(string permissionCode);

    /// <summary>Check if the current user has ALL of the specified permissions.</summary>
    Task<bool> HasAllPermissionsAsync(params string[] permissionCodes);

    /// <summary>Check if the current user has ANY of the specified permissions.</summary>
    Task<bool> HasAnyPermissionAsync(params string[] permissionCodes);

    /// <summary>Get the full permission set for the current user.</summary>
    Task<UserPermissionSet> GetCurrentUserPermissionsAsync();

    /// <summary>Get permissions for a specific user+tenant (used by AG ONE gateway for token issuance).</summary>
    Task<UserPermissionSet> GetUserPermissionsAsync(Guid userId, Guid tenantId);

    /// <summary>
    /// Invalidate cached permissions for a user. Call when roles/permissions change.
    /// On the gateway, also bumps the version number so downstream apps detect the change.
    /// </summary>
    Task InvalidateUserPermissionsAsync(Guid userId, Guid tenantId);
}

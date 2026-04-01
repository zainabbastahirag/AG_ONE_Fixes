namespace AGOne.Authorization.Services;

/// <summary>
/// Extracts current user identity from the HTTP context / JWT claims.
/// </summary>
public interface ICurrentUserAccessor
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    long? PermissionVersion { get; }
    IReadOnlySet<string> JwtPermissions { get; }
    bool IsAuthenticated { get; }
}

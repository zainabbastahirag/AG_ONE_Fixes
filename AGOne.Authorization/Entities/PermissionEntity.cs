namespace AGOne.Authorization.Entities;

public class PermissionEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Group { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Action { get; set; } = "";
    public string Product { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RoleEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RolePermissionEntity> RolePermissions { get; set; } = new List<RolePermissionEntity>();
    public ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
}

public class RolePermissionEntity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public RoleEntity Role { get; set; } = null!;
    public PermissionEntity Permission { get; set; } = null!;
}

public class UserRoleEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public RoleEntity Role { get; set; } = null!;
}

/// <summary>
/// Tracks permission version per user+tenant. Incremented whenever roles/permissions change.
/// Downstream apps compare this against the JWT's perm_version claim to know when to refresh.
/// </summary>
public class UserPermissionVersionEntity
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public long Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════════════════
// FILE: AssignAccessDtos.cs
// GOES IN: Shared project (AGOne.Shared/DTOs/)
// ═══════════════════════════════════════════════════════════════════════════

namespace AGOne.Shared.DTOs;

public class AssignAccessUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class AssignAccessProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

public class AssignAccessRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public Guid? ProductId { get; set; }
    public bool IsSystemRole { get; set; }
}

public class AssignAccessEntityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public class ProductRoleAssignment
{
    public Guid ProductId { get; set; }
    public Guid RoleId { get; set; }
}

public class AssignAccessRequest
{
    public List<Guid> UserIds { get; set; } = new();
    public List<ProductRoleAssignment> Assignments { get; set; } = new();
}

public class AssignAccessResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int AssignedCount { get; set; }
    public int SkippedCount { get; set; }
}

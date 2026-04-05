// ═══════════════════════════════════════════════════════════════════════════
// FILE: AssignAccessDtos.cs
// GOES IN: Shared project (AGOne.Shared)
// Namespace: AGOne.Shared.DTOs
//
// DTOs for the Assign User Access wizard (/admin/users/assign-access)
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AGOne.Shared.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// READ DTOs — returned by API, consumed by UI
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>User DTO for the assign-access user search dropdown.</summary>
public class AssignAccessUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
        ? Email
        : $"{FirstName} {LastName}".Trim();

    public string Initials
    {
        get
        {
            var f = FirstName?.Length > 0 ? FirstName[0].ToString().ToUpper() : "";
            var l = LastName?.Length > 0 ? LastName[0].ToString().ToUpper() : "";
            return string.IsNullOrEmpty(f + l) ? Email[..1].ToUpper() : f + l;
        }
    }
}

/// <summary>Product DTO for the assign-access product checklist.</summary>
public class AssignAccessProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>Role DTO for the assign-access role checklist (filtered by product).</summary>
public class AssignAccessRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ProductId { get; set; }
    public bool IsSystemRole { get; set; }
}

/// <summary>Entity (tenant/branch) for the entity dropdown in Step 1.</summary>
public class AssignAccessEntityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════════════════
// WRITE DTOs — sent from UI to API
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>A single product ↔ role assignment pair.</summary>
public class ProductRoleAssignment
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public Guid RoleId { get; set; }
}

/// <summary>Request body for POST /api/users/access/assign.</summary>
public class AssignAccessRequest
{
    /// <summary>One or more user IDs to assign access to.</summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one user must be selected.")]
    public List<Guid> UserIds { get; set; } = new();

    /// <summary>Product + role combinations to assign to each user.</summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one product-role assignment is required.")]
    public List<ProductRoleAssignment> Assignments { get; set; } = new();
}

/// <summary>Response from POST /api/users/access/assign.</summary>
public class AssignAccessResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int AssignedCount { get; set; }
    public int SkippedDuplicateCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════
// SERVICE INTERFACE — implemented in Infrastructure, consumed by API
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Service contract for user access assignment operations.</summary>
public interface IUserAccessService
{
    Task<List<AssignAccessEntityDto>> GetEntitiesAsync(Guid tenantId);
    Task<List<AssignAccessUserDto>> SearchUsersAsync(Guid tenantId, string? search, Guid? entityId);
    Task<List<AssignAccessProductDto>> GetProductsAsync(Guid tenantId);
    Task<List<AssignAccessRoleDto>> GetRolesAsync(Guid tenantId, Guid? productId);
    Task<AssignAccessResponse> AssignAccessAsync(Guid tenantId, AssignAccessRequest request);
}

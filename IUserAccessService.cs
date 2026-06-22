// ═══════════════════════════════════════════════════════════════════════════
// FILE: IUserAccessService.cs
// GOES IN: Shared project (AGOne.Shared/Interfaces/)
// ═══════════════════════════════════════════════════════════════════════════

using AGOne.Shared.DTOs;

namespace AGOne.Shared.Interfaces;

public interface IUserAccessService
{
    Task<List<AssignAccessEntityDto>> GetEntitiesAsync(Guid tenantId);
    Task<List<AssignAccessUserDto>> SearchUsersAsync(Guid tenantId, string? search);
    Task<List<AssignAccessProductDto>> GetProductsAsync(Guid tenantId);
    Task<List<AssignAccessRoleDto>> GetRolesAsync(Guid tenantId, Guid? productId);
    Task<AssignAccessResponse> AssignAccessAsync(Guid tenantId, AssignAccessRequest request);
    Task<List<UserExistingRoleDto>> GetUserExistingRolesAsync(Guid tenantId, Guid userId);
}

public class UserExistingRoleDto
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string RoleDisplayName { get; set; } = "";
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
}

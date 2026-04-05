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
}

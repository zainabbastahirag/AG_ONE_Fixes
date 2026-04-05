// ═══════════════════════════════════════════════════════════════════════════
// FILE: UserAccessService.cs
// GOES IN: Infrastructure project (AGOne.Infrastructure/Services/)
//
// Uses your existing AGOneDbContext with these DbSets:
//   _db.Users, _db.Roles, _db.UserRoles, _db.Products, _db.Tenants
// All tables in [core] schema. All have IsDeleted soft-delete.
// ═══════════════════════════════════════════════════════════════════════════

using AGOne.Shared.DTOs;
using AGOne.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AGOne.Infrastructure.Services;

public class UserAccessService : IUserAccessService
{
    private readonly AGOneDbContext _db;

    public UserAccessService(AGOneDbContext db)
    {
        _db = db;
    }

    public async Task<List<AssignAccessEntityDto>> GetEntitiesAsync(Guid tenantId)
    {
        // Returns the tenant itself as the entity (expand if you have branches/entities table)
        var tenant = await _db.Tenants
            .Where(t => t.Id == tenantId && !t.IsDeleted)
            .Select(t => new AssignAccessEntityDto { Id = t.Id, Name = t.Name })
            .FirstOrDefaultAsync();

        return tenant != null ? new List<AssignAccessEntityDto> { tenant } : new();
    }

    public async Task<List<AssignAccessUserDto>> SearchUsersAsync(Guid tenantId, string? search)
    {
        var query = _db.Users
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term));
        }

        return await query
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Take(50)
            .Select(u => new AssignAccessUserDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                AvatarUrl = u.AvatarUrl
            })
            .ToListAsync();
    }

    public async Task<List<AssignAccessProductDto>> GetProductsAsync(Guid tenantId)
    {
        // Platform product (AG ONE admin) + all products that have active subscriptions
        var products = new List<AssignAccessProductDto>
        {
            new() { Id = Guid.Empty, Name = "AG ONE (admin)", Code = "AGOne" }
        };

        var subscribed = await _db.Subscriptions
            .Where(s => s.TenantId == tenantId && s.Status == "Active" && !s.IsDeleted)
            .Include(s => s.Product)
            .Where(s => s.Product != null && !s.Product.IsDeleted)
            .Select(s => new AssignAccessProductDto
            {
                Id = s.Product!.Id,
                Name = s.Product.Name,
                Code = s.Product.Code
            })
            .Distinct()
            .ToListAsync();

        products.AddRange(subscribed);
        return products;
    }

    public async Task<List<AssignAccessRoleDto>> GetRolesAsync(Guid tenantId, Guid? productId)
    {
        var query = _db.Roles
            .Where(r => !r.IsDeleted && !r.IsSystemRole)
            .Where(r => r.TenantId == tenantId || r.TenantId == null);

        if (productId.HasValue)
        {
            if (productId.Value == Guid.Empty)
            {
                // Platform roles (no product)
                query = query.Where(r => r.ProductId == null);
            }
            else
            {
                query = query.Where(r => r.ProductId == productId.Value);
            }
        }

        return await query
            .OrderBy(r => r.Name)
            .Select(r => new AssignAccessRoleDto
            {
                Id = r.Id,
                Name = r.Name,
                DisplayName = r.DisplayName,
                Description = r.Description,
                ProductId = r.ProductId,
                IsSystemRole = r.IsSystemRole
            })
            .ToListAsync();
    }

    public async Task<AssignAccessResponse> AssignAccessAsync(Guid tenantId, AssignAccessRequest request)
    {
        if (!request.UserIds.Any() || !request.Assignments.Any())
            return new AssignAccessResponse { Success = false, Message = "No users or assignments provided." };

        int inserted = 0;
        int skipped = 0;

        foreach (var userId in request.UserIds)
        {
            // Verify user exists in this tenant
            var userExists = await _db.Users.AnyAsync(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted);
            if (!userExists) { skipped++; continue; }

            // Get existing active roles for this user
            var existingRoleIds = await _db.UserRoles
                .Where(ur => ur.UserId == userId && ur.TenantId == tenantId && !ur.IsDeleted)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            var existingSet = existingRoleIds.ToHashSet();

            // Get current max priority
            var maxPriority = await _db.UserRoles
                .Where(ur => ur.UserId == userId && ur.TenantId == tenantId && !ur.IsDeleted)
                .MaxAsync(ur => (int?)ur.Priority) ?? 0;

            foreach (var assignment in request.Assignments)
            {
                if (existingSet.Contains(assignment.RoleId))
                {
                    skipped++;
                    continue;
                }

                var productId = assignment.ProductId == Guid.Empty ? null : (Guid?)assignment.ProductId;

                maxPriority += 10;
                _db.UserRoles.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RoleId = assignment.RoleId,
                    TenantId = tenantId,
                    ProductId = productId,
                    Priority = maxPriority,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                });

                existingSet.Add(assignment.RoleId);
                inserted++;
            }
        }

        await _db.SaveChangesAsync();

        return new AssignAccessResponse
        {
            Success = true,
            Message = $"Assigned {inserted} role(s). Skipped {skipped} (already assigned or user not found).",
            AssignedCount = inserted,
            SkippedCount = skipped
        };
    }

    public async Task<List<UserExistingRoleDto>> GetUserExistingRolesAsync(Guid tenantId, Guid userId)
    {
        return await _db.UserRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId && !ur.IsDeleted)
            .Join(_db.Roles.Where(r => !r.IsDeleted), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur, r })
            .Select(x => new UserExistingRoleDto
            {
                RoleId = x.r.Id,
                RoleName = x.r.Name,
                RoleDisplayName = x.r.DisplayName,
                ProductId = x.r.ProductId,
                ProductName = x.r.Product != null ? x.r.Product.Name : null
            })
            .ToListAsync();
    }
}

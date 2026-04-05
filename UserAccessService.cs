// ═══════════════════════════════════════════════════════════════════════════
// FILE: UserAccessService.cs
// GOES IN: Infrastructure project (AGOne.Infrastructure)
// Namespace: AGOne.Infrastructure.Services
//
// Implements IUserAccessService using EF Core with AGOneDbContext.
// Tables referenced (all in [core] schema):
//   core.Users, core.Roles, core.UserRoles, core.Products, core.Tenants,
//   core.Subscriptions (for product access)
//
// Depends on:
//   - AGOneDbContext (assumed available via DI as _db)
//   - IUserAccessService interface (from Shared project)
//   - AssignAccess DTOs (from Shared project)
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AGOne.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AGOne.Infrastructure.Services;

public class UserAccessService : IUserAccessService
{
    private readonly AGOneDbContext _db;
    private readonly ILogger<UserAccessService> _logger;

    public UserAccessService(AGOneDbContext db, ILogger<UserAccessService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns tenant entities for the entity dropdown.
    /// In this system, each Tenant row represents an entity/branch.
    /// The current tenant plus any child tenants are returned.
    /// </summary>
    public async Task<List<AssignAccessEntityDto>> GetEntitiesAsync(Guid tenantId)
    {
        var entities = await _db.Tenants
            .Where(t => (t.Id == tenantId || t.ParentTenantId == tenantId) && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .Select(t => new AssignAccessEntityDto
            {
                Id = t.Id,
                Name = t.Name
            })
            .ToListAsync();

        return entities;
    }

    /// <summary>
    /// Searches users within the tenant (or a specific entity/child tenant).
    /// Returns up to 50 active, non-deleted users matching the optional search term.
    /// </summary>
    public async Task<List<AssignAccessUserDto>> SearchUsersAsync(
        Guid tenantId, string? search, Guid? entityId)
    {
        var targetTenantId = entityId ?? tenantId;

        var query = _db.Users
            .Where(u => u.TenantId == targetTenantId && u.IsActive && !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                (u.FirstName + " " + u.LastName).ToLower().Contains(term));
        }

        var users = await query
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

        return users;
    }

    /// <summary>
    /// Returns products that the tenant has active subscriptions for.
    /// </summary>
    public async Task<List<AssignAccessProductDto>> GetProductsAsync(Guid tenantId)
    {
        var products = await _db.Subscriptions
            .Where(s => s.TenantId == tenantId && s.Status == "Active" && !s.IsDeleted)
            .Join(_db.Products,
                  sub => sub.ProductId,
                  prod => prod.Id,
                  (sub, prod) => new AssignAccessProductDto
                  {
                      Id = prod.Id,
                      Name = prod.Name,
                      Code = prod.Code
                  })
            .Distinct()
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products;
    }

    /// <summary>
    /// Returns roles available within the tenant, optionally filtered by product.
    /// Includes platform-level roles (ProductId == null) when no productId filter is applied.
    /// </summary>
    public async Task<List<AssignAccessRoleDto>> GetRolesAsync(Guid tenantId, Guid? productId)
    {
        var query = _db.Roles
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

        if (productId.HasValue)
        {
            query = query.Where(r => r.ProductId == productId.Value || r.ProductId == null);
        }

        var roles = await query
            .OrderBy(r => r.ProductId == null ? 0 : 1)
            .ThenBy(r => r.DisplayName)
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

        return roles;
    }

    /// <summary>
    /// Assigns the specified product-role combinations to the specified users.
    /// Skips any UserRole entries that already exist (duplicate detection).
    /// Creates new UserRole records within a transaction for atomicity.
    /// </summary>
    public async Task<AssignAccessResponse> AssignAccessAsync(
        Guid tenantId, AssignAccessRequest request)
    {
        var response = new AssignAccessResponse();
        var errors = new List<string>();
        var insertedCount = 0;
        var skippedCount = 0;

        try
        {
            // Validate users exist and belong to the tenant
            var validUserIds = await _db.Users
                .Where(u => request.UserIds.Contains(u.Id) && u.TenantId == tenantId && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync();

            var invalidUserIds = request.UserIds.Except(validUserIds).ToList();
            if (invalidUserIds.Any())
            {
                errors.Add($"{invalidUserIds.Count} user(s) not found or do not belong to this tenant.");
            }

            // Validate roles exist
            var roleIds = request.Assignments.Select(a => a.RoleId).Distinct().ToList();
            var validRoleIds = await _db.Roles
                .Where(r => roleIds.Contains(r.Id) && r.TenantId == tenantId && !r.IsDeleted)
                .Select(r => r.Id)
                .ToListAsync();

            var invalidRoleIds = roleIds.Except(validRoleIds).ToList();
            if (invalidRoleIds.Any())
            {
                errors.Add($"{invalidRoleIds.Count} role(s) not found or do not belong to this tenant.");
            }

            if (!validUserIds.Any())
            {
                return new AssignAccessResponse
                {
                    Success = false,
                    Message = "No valid users to assign access to.",
                    Errors = errors
                };
            }

            // Fetch existing UserRole combinations to detect duplicates
            var existingAssignments = await _db.UserRoles
                .Where(ur => validUserIds.Contains(ur.UserId) &&
                             ur.TenantId == tenantId &&
                             !ur.IsDeleted)
                .Select(ur => new { ur.UserId, ur.RoleId, ur.ProductId })
                .ToListAsync();

            var existingSet = existingAssignments
                .Select(e => (e.UserId, e.RoleId, e.ProductId))
                .ToHashSet();

            var now = DateTime.UtcNow;
            var newUserRoles = new List<UserRoleEntity>();

            // Determine max priority per user to continue the sequence
            var maxPriorities = await _db.UserRoles
                .Where(ur => validUserIds.Contains(ur.UserId) && ur.TenantId == tenantId && !ur.IsDeleted)
                .GroupBy(ur => ur.UserId)
                .Select(g => new { UserId = g.Key, MaxPriority = g.Max(ur => ur.Priority) })
                .ToDictionaryAsync(x => x.UserId, x => x.MaxPriority);

            foreach (var userId in validUserIds)
            {
                var priority = maxPriorities.GetValueOrDefault(userId, 0);

                foreach (var assignment in request.Assignments)
                {
                    if (!validRoleIds.Contains(assignment.RoleId))
                        continue;

                    // Check for existing assignment (duplicate detection)
                    if (existingSet.Contains((userId, assignment.RoleId, assignment.ProductId)))
                    {
                        skippedCount++;
                        continue;
                    }

                    priority++;
                    newUserRoles.Add(new UserRoleEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        RoleId = assignment.RoleId,
                        TenantId = tenantId,
                        ProductId = assignment.ProductId,
                        Priority = priority,
                        CreatedAt = now,
                        IsDeleted = false
                    });
                    insertedCount++;
                }
            }

            if (newUserRoles.Any())
            {
                _db.UserRoles.AddRange(newUserRoles);
                await _db.SaveChangesAsync();
            }

            response.Success = true;
            response.AssignedCount = insertedCount;
            response.SkippedDuplicateCount = skippedCount;
            response.Message = insertedCount > 0
                ? $"Successfully assigned {insertedCount} role(s) to {validUserIds.Count} user(s)."
                  + (skippedCount > 0 ? $" {skippedCount} duplicate assignment(s) skipped." : "")
                : skippedCount > 0
                    ? $"All {skippedCount} assignment(s) already exist. No changes made."
                    : "No assignments were made.";
            response.Errors = errors;

            _logger.LogInformation(
                "AssignAccess completed: {Inserted} inserted, {Skipped} skipped for tenant {TenantId}",
                insertedCount, skippedCount, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AssignAccessAsync for tenant {TenantId}", tenantId);
            response.Success = false;
            response.Message = "An unexpected error occurred while assigning access.";
            response.Errors.Add(ex.Message);
        }

        return response;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// EF CORE ENTITY STUBS
//
// These represent the EF entities that already exist in the real project.
// They are included here as reference for the shape of the data model.
// In the actual project, remove these and reference the real entities.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Represents a row in core.UserRoles</summary>
public class UserRoleEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ProductId { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// DbContext stub — represents the shape of the actual AGOneDbContext.
// In the real project, this already exists with all DbSet properties.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Stub DbContext — replace with your actual AGOneDbContext.
/// All tables use the [core] schema.
/// </summary>
public class AGOneDbContext : DbContext
{
    public AGOneDbContext(DbContextOptions<AGOneDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users { get; set; } = null!;
    public DbSet<RoleEntity> Roles { get; set; } = null!;
    public DbSet<UserRoleEntity> UserRoles { get; set; } = null!;
    public DbSet<ProductEntity> Products { get; set; } = null!;
    public DbSet<TenantEntity> Tenants { get; set; } = null!;
    public DbSet<SubscriptionEntity> Subscriptions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<UserEntity>().ToTable("Users");
        modelBuilder.Entity<RoleEntity>().ToTable("Roles");
        modelBuilder.Entity<UserRoleEntity>().ToTable("UserRoles");
        modelBuilder.Entity<ProductEntity>().ToTable("Products");
        modelBuilder.Entity<TenantEntity>().ToTable("Tenants");
        modelBuilder.Entity<SubscriptionEntity>().ToTable("Subscriptions");
    }
}

public class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public Guid TenantId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

public class RoleEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public Guid? ProductId { get; set; }
    public Guid TenantId { get; set; }
    public bool IsDeleted { get; set; }
}

public class ProductEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

public class TenantEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid? ParentTenantId { get; set; }
    public bool IsDeleted { get; set; }
}

public class SubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Status { get; set; } = "";
    public bool IsDeleted { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// DI EXTENSION — call from API Program.cs
// ═══════════════════════════════════════════════════════════════════════════

public static class UserAccessServiceExtensions
{
    public static IServiceCollection AddUserAccessService(this IServiceCollection services)
    {
        services.AddScoped<IUserAccessService, UserAccessService>();
        return services;
    }
}

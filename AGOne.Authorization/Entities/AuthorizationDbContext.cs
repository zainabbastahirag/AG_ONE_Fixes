using Microsoft.EntityFrameworkCore;

namespace AGOne.Authorization.Entities;

public class AuthorizationDbContext : DbContext
{
    public AuthorizationDbContext(DbContextOptions<AuthorizationDbContext> options) : base(options) { }

    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<UserPermissionVersionEntity> UserPermissionVersions => Set<UserPermissionVersionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PermissionEntity>(e =>
        {
            e.ToTable("Permissions");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Code).HasMaxLength(128).IsRequired();
            e.Property(p => p.DisplayName).HasMaxLength(256);
            e.Property(p => p.Group).HasMaxLength(128);
            e.Property(p => p.Resource).HasMaxLength(128);
            e.Property(p => p.Action).HasMaxLength(64);
            e.Property(p => p.Product).HasMaxLength(128);
        });

        modelBuilder.Entity<RoleEntity>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
            e.Property(r => r.Name).HasMaxLength(256).IsRequired();
            e.Property(r => r.Description).HasMaxLength(1024);
        });

        modelBuilder.Entity<RolePermissionEntity>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Permission).WithMany().HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRoleEntity>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(ur => new { ur.UserId, ur.RoleId, ur.TenantId });
            e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPermissionVersionEntity>(e =>
        {
            e.ToTable("UserPermissionVersions");
            e.HasKey(v => new { v.UserId, v.TenantId });
        });
    }
}

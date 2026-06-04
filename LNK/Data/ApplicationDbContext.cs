using LNK.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LNK.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserSettings>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithOne(x => x.Settings).HasForeignKey<UserSettings>(x => x.UserId);
        });

        builder.Entity<Post>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.GeneratedAt);
        });

        builder.Entity<Schedule>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithOne(x => x.Schedule).HasForeignKey<Schedule>(x => x.UserId);
        });

        builder.Entity<EmailLog>(e => e.HasIndex(x => x.SentAt));

        builder.Entity<AppSetting>(e => e.HasIndex(x => x.Key).IsUnique());
    }
}

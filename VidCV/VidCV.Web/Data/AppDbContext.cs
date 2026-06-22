using Microsoft.EntityFrameworkCore;
using VidCV.Web.Models;

namespace VidCV.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CvProfile> CvProfiles => Set<CvProfile>();
    public DbSet<VideoTemplate> VideoTemplates => Set<VideoTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CvProfile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.LinkedInUrl).HasMaxLength(500);
            e.Property(x => x.JobTitle).HasMaxLength(200);
            e.Property(x => x.Summary).HasMaxLength(2000);
            e.Property(x => x.Skills).HasMaxLength(2000);
            e.Property(x => x.Experience).HasMaxLength(4000);
            e.Property(x => x.Education).HasMaxLength(2000);
            e.Property(x => x.CvFileName).HasMaxLength(300);
            e.Property(x => x.CvFilePath).HasMaxLength(500);
            e.Property(x => x.VideoScript).HasMaxLength(4000);
            e.Property(x => x.VideoPath).HasMaxLength(500);
            e.Property(x => x.VideoUrl).HasMaxLength(500);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<VideoTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.BackgroundColor).HasMaxLength(20);
            e.Property(x => x.AccentColor).HasMaxLength(20);
            e.Property(x => x.TextColor).HasMaxLength(20);
            e.Property(x => x.FontFamily).HasMaxLength(100);
        });
    }
}

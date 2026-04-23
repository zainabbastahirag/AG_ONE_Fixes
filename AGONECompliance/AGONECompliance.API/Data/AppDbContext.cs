using AGONECompliance.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AGONECompliance.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ComplianceProject> Projects => Set<ComplianceProject>();
    public DbSet<ProjectDocument> Documents => Set<ProjectDocument>();
    public DbSet<GuidelineRule> Rules => Set<GuidelineRule>();
    public DbSet<ComplianceCheck> Checks => Set<ComplianceCheck>();
    public DbSet<JobActivity> Activities => Set<JobActivity>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("compliance");

        mb.Entity<ComplianceProject>(e =>
        {
            e.ToTable("Projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasIndex(x => x.CreatedAt);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        mb.Entity<ProjectDocument>(e =>
        {
            e.ToTable("Documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(500).IsRequired();
            e.Property(x => x.BlobPath).HasMaxLength(1000);
            e.Property(x => x.BlobUrl).HasMaxLength(2000);
            e.HasOne(x => x.Project).WithMany(p => p.Documents).HasForeignKey(x => x.ProjectId);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        mb.Entity<GuidelineRule>(e =>
        {
            e.ToTable("Rules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(100);
            e.Property(x => x.Paragraph).HasMaxLength(500);
            e.Property(x => x.Requirement).HasMaxLength(4000);
            e.Property(x => x.Group).HasMaxLength(200);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
            e.HasIndex(x => new { x.ProjectId, x.RuleNumber });
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        mb.Entity<ComplianceCheck>(e =>
        {
            e.ToTable("Checks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Finding).HasMaxLength(4000);
            e.Property(x => x.Evidence).HasMaxLength(4000);
            e.Property(x => x.PageReference).HasMaxLength(200);
            e.Property(x => x.SectionReference).HasMaxLength(500);
            e.HasOne(x => x.Project).WithMany(p => p.Checks).HasForeignKey(x => x.ProjectId);
            e.HasOne(x => x.Rule).WithMany(r => r.Checks).HasForeignKey(x => x.RuleId);
            e.HasOne(x => x.ProspectusDoc).WithMany().HasForeignKey(x => x.ProspectusDocId);
            e.HasIndex(x => new { x.ProjectId, x.Result });
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        mb.Entity<JobActivity>(e =>
        {
            e.ToTable("JobActivities");
            e.HasKey(x => x.Id);
            e.Property(x => x.JobType).HasMaxLength(100);
            e.Property(x => x.Step).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.Message).HasMaxLength(2000);
            e.Property(x => x.ErrorDetail).HasMaxLength(4000);
            e.HasIndex(x => new { x.ProjectId, x.StartedAt });
        });
    }
}

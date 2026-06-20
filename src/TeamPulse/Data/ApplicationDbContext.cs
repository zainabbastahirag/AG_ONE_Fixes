using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Models.Domain;

namespace TeamPulse.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<ReviewerAssignment> ReviewerAssignments => Set<ReviewerAssignment>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Team>()
            .HasOne(t => t.TechLead)
            .WithMany(u => u.LedTeams)
            .HasForeignKey(t => t.TechLeadUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Member>()
            .HasOne(m => m.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Member>()
            .HasOne(m => m.ApplicationUser)
            .WithMany()
            .HasForeignKey(m => m.ApplicationUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<WorkItem>()
            .HasOne(w => w.Team)
            .WithMany(t => t.WorkItems)
            .HasForeignKey(w => w.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<WorkItem>()
            .HasOne(w => w.AssignedMember)
            .WithMany(m => m.AssignedWorkItems)
            .HasForeignKey(w => w.AssignedMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<WorkItem>()
            .HasOne(w => w.Sprint)
            .WithMany(s => s.WorkItems)
            .HasForeignKey(w => w.SprintId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Release>()
            .HasOne(r => r.Team)
            .WithMany(t => t.Releases)
            .HasForeignKey(r => r.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<PerformanceReview>()
            .HasOne(p => p.Member)
            .WithMany(m => m.Reviews)
            .HasForeignKey(p => p.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PerformanceReview>()
            .HasOne(p => p.Reviewer)
            .WithMany(u => u.AuthoredReviews)
            .HasForeignKey(p => p.ReviewerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<PerformanceReview>()
            .HasOne(p => p.Sprint)
            .WithMany(s => s.Reviews)
            .HasForeignKey(p => p.SprintId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ReviewerAssignment>()
            .HasOne(r => r.Reviewer)
            .WithMany(u => u.ReviewerAssignments)
            .HasForeignKey(r => r.ReviewerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ReviewerAssignment>()
            .HasOne(r => r.Member)
            .WithMany()
            .HasForeignKey(r => r.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ReviewerAssignment>()
            .HasIndex(r => new { r.ReviewerUserId, r.MemberId })
            .IsUnique();

        builder.Entity<Invitation>()
            .HasIndex(i => i.Token)
            .IsUnique();

        builder.Entity<PerformanceReview>().Ignore(p => p.OverallRating);
    }
}

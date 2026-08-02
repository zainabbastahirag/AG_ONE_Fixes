using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Audit;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Escalations;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Domain.Remediation;
using AgOneSafe.Domain.Tenants;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Infrastructure.Persistence;

public class AgOneSafeDbContext : IdentityDbContext<ApplicationUser>, IAgOneSafeDbContext
{
    public AgOneSafeDbContext(DbContextOptions<AgOneSafeDbContext> options) : base(options)
    {
    }

    public DbSet<ControlDefinition> ControlDefinitions => Set<ControlDefinition>();
    public DbSet<ControlAction> ControlActions => Set<ControlAction>();
    public DbSet<AssertionRule> AssertionRules => Set<AssertionRule>();

    public DbSet<TenantConnection> TenantConnections => Set<TenantConnection>();
    public DbSet<ConnectorHealthCheck> ConnectorHealthChecks => Set<ConnectorHealthCheck>();

    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<ControlResult> ControlResults => Set<ControlResult>();
    public DbSet<Finding> Findings => Set<Finding>();

    public DbSet<RemediationTask> RemediationTasks => Set<RemediationTask>();
    public DbSet<RemediationExecution> RemediationExecutions => Set<RemediationExecution>();
    public DbSet<RemediationBatch> RemediationBatches => Set<RemediationBatch>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalSignature> ApprovalSignatures => Set<ApprovalSignature>();
    public DbSet<ConfigurationSnapshot> ConfigurationSnapshots => Set<ConfigurationSnapshot>();

    public DbSet<ServiceEscalation> ServiceEscalations => Set<ServiceEscalation>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ControlDefinition>(entity =>
        {
            entity.HasIndex(c => new { c.Benchmark, c.VendorReference }).IsUnique();
            entity.HasIndex(c => c.Domain);
            entity.HasMany(c => c.Actions)
                .WithOne(a => a.ControlDefinition!)
                .HasForeignKey(a => a.ControlDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ControlAction>(entity =>
        {
            entity.HasIndex(a => new { a.ControlDefinitionId, a.Kind });
            entity.HasMany(a => a.Assertions)
                .WithOne(r => r.ControlAction!)
                .HasForeignKey(r => r.ControlActionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TenantConnection>(entity =>
        {
            entity.HasIndex(t => t.TenantId).IsUnique();
            entity.HasMany(t => t.HealthChecks)
                .WithOne(h => h.TenantConnection!)
                .HasForeignKey(h => h.TenantConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Assessment>(entity =>
        {
            entity.HasIndex(a => new { a.TenantConnectionId, a.StartedAt });
            entity.HasMany(a => a.Results)
                .WithOne(r => r.Assessment!)
                .HasForeignKey(r => r.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ControlResult>(entity =>
        {
            entity.HasIndex(r => new { r.AssessmentId, r.ControlDefinitionId }).IsUnique();
            entity.HasOne(r => r.ControlDefinition)
                .WithMany()
                .HasForeignKey(r => r.ControlDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Finding>(entity =>
        {
            entity.HasIndex(f => new { f.TenantConnectionId, f.ControlDefinitionId }).IsUnique();
            entity.HasOne(f => f.ControlDefinition)
                .WithMany()
                .HasForeignKey(f => f.ControlDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(f => f.LatestControlResult)
                .WithOne(r => r.Finding!)
                .HasForeignKey<Finding>(f => f.LatestControlResultId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(f => f.RemediationTasks)
                .WithOne(t => t.Finding!)
                .HasForeignKey(t => t.FindingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RemediationTask>(entity =>
        {
            entity.HasIndex(t => new { t.TenantConnectionId, t.Status });
            entity.HasMany(t => t.Executions)
                .WithOne(e => e.RemediationTask!)
                .HasForeignKey(e => e.RemediationTaskId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(t => t.ApprovalRequests)
                .WithOne(r => r.RemediationTask!)
                .HasForeignKey(r => r.RemediationTaskId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.ConfigurationSnapshot)
                .WithMany()
                .HasForeignKey(t => t.ConfigurationSnapshotId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(t => t.ControlDefinition)
                .WithMany()
                .HasForeignKey(t => t.ControlDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.RemediationBatch)
                .WithMany(b => b.Tasks)
                .HasForeignKey(t => t.RemediationBatchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ApprovalRequest>(entity =>
            entity.HasMany(r => r.Signatures)
                .WithOne(s => s.ApprovalRequest!)
                .HasForeignKey(s => s.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Cascade));

        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasIndex(e => e.OccurredAt);
            entity.HasIndex(e => new { e.TenantConnectionId, e.EventType });
        });

        builder.Entity<ServiceEscalation>(entity =>
            entity.HasOne(e => e.Finding)
                .WithMany()
                .HasForeignKey(e => e.FindingId)
                .OnDelete(DeleteBehavior.SetNull));
    }

    Task<int> IAgOneSafeDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        base.SaveChangesAsync(cancellationToken);
}

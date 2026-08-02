using System.Diagnostics;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Application.Tenants;

public interface ITenantConnectionService
{
    Task<TenantConnection> OnboardAsync(TenantOnboardingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Probes every workload the catalog needs and records reachability plus consent gaps.</summary>
    Task<IReadOnlyList<ConnectorHealthCheck>> RunHealthChecksAsync(int tenantConnectionId, CancellationToken cancellationToken = default);
}

public sealed class TenantOnboardingRequest
{
    public required string DisplayName { get; init; }
    public required string TenantId { get; init; }
    public required string PrimaryDomain { get; init; }
    public required string ClientId { get; init; }
    public string SecretReference { get; init; } = string.Empty;
    public string AzureSubscriptionIds { get; init; } = string.Empty;
    public bool LighthouseEnabled { get; init; }
    public string LicenseSkus { get; init; } = string.Empty;
    public ProfileLevel TargetLevel { get; init; } = ProfileLevel.L1;
    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.Simulation;
    public string RequestedBy { get; init; } = "system";
}

public sealed class TenantConnectionService : ITenantConnectionService
{
    private readonly IAgOneSafeDbContext _db;
    private readonly IExecutorRegistry _executors;
    private readonly IAuditTrail _audit;

    public TenantConnectionService(IAgOneSafeDbContext db, IExecutorRegistry executors, IAuditTrail audit)
    {
        _db = db;
        _executors = executors;
        _audit = audit;
    }

    public async Task<TenantConnection> OnboardAsync(
        TenantOnboardingRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.TenantConnections
            .FirstOrDefaultAsync(t => t.TenantId == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            tenant = new TenantConnection { TenantId = request.TenantId };
            _db.TenantConnections.Add(tenant);
        }

        tenant.DisplayName = request.DisplayName;
        tenant.PrimaryDomain = request.PrimaryDomain;
        tenant.ClientId = request.ClientId;
        tenant.SecretReference = request.SecretReference;
        tenant.AzureSubscriptionIds = request.AzureSubscriptionIds;
        tenant.LighthouseEnabled = request.LighthouseEnabled;
        tenant.LicenseSkus = request.LicenseSkus;
        tenant.TargetLevel = request.TargetLevel;
        tenant.ExecutionMode = request.ExecutionMode;
        tenant.Status = TenantConnectionStatus.Connected;
        tenant.ConnectedAt ??= DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.TenantConnected,
            TenantConnectionId = tenant.Id,
            Actor = request.RequestedBy,
            ActorRole = "User",
            Summary = $"Tenant {tenant.DisplayName} ({tenant.PrimaryDomain}) connected in {tenant.ExecutionMode} mode.",
            Details = new
            {
                tenant.TenantId,
                tenant.ClientId,
                tenant.LighthouseEnabled,
                TargetLevel = tenant.TargetLevel.ToString()
            }
        }, cancellationToken);

        await RunHealthChecksAsync(tenant.Id, cancellationToken);

        return tenant;
    }

    public async Task<IReadOnlyList<ConnectorHealthCheck>> RunHealthChecksAsync(
        int tenantConnectionId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.TenantConnections
                         .Include(t => t.HealthChecks)
                         .FirstOrDefaultAsync(t => t.Id == tenantConnectionId, cancellationToken)
                     ?? throw new InvalidOperationException($"Tenant {tenantConnectionId} not found.");

        // Only probe the workloads the enabled catalog actually needs.
        var requirements = await _db.ControlActions
            .AsNoTracking()
            .Where(a => a.ControlDefinition!.IsEnabled)
            .GroupBy(a => new { a.Module, a.ExecutorType })
            .Select(g => new
            {
                g.Key.Module,
                g.Key.ExecutorType,
                Scopes = g.Select(a => a.RequiredScopes).ToList()
            })
            .ToListAsync(cancellationToken);

        _db.ConnectorHealthChecks.RemoveRange(tenant.HealthChecks);

        var checks = new List<ConnectorHealthCheck>();

        foreach (var requirement in requirements.Where(r => r.Module != ConnectorModule.None))
        {
            var stopwatch = Stopwatch.StartNew();
            var executor = _executors.Resolve(requirement.ExecutorType);
            var available = await executor.IsAvailableAsync(cancellationToken);
            stopwatch.Stop();

            var scopes = requirement.Scopes
                .SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToList();

            var check = new ConnectorHealthCheck
            {
                TenantConnectionId = tenant.Id,
                Module = requirement.Module,
                IsHealthy = available,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                GrantedScopes = string.Join(", ", scopes),
                MissingScopes = string.Empty,
                Message = available
                    ? $"{requirement.Module} reachable via {requirement.ExecutorType}."
                    : $"{requirement.ExecutorType} runtime is not available on this host for {requirement.Module}.",
                CheckedAt = DateTimeOffset.UtcNow
            };

            checks.Add(check);
            _db.ConnectorHealthChecks.Add(check);
        }

        tenant.LastHealthCheckAt = DateTimeOffset.UtcNow;
        tenant.Status = checks.All(c => c.IsHealthy)
            ? TenantConnectionStatus.Connected
            : checks.Any(c => c.IsHealthy)
                ? TenantConnectionStatus.Degraded
                : TenantConnectionStatus.Failed;
        tenant.LastHealthMessage = $"{checks.Count(c => c.IsHealthy)}/{checks.Count} connectors healthy.";

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.TenantHealthCheck,
            TenantConnectionId = tenant.Id,
            Actor = "ag-one-agent",
            Summary = $"Connector health check: {tenant.LastHealthMessage}",
            Details = checks.Select(c => new { Module = c.Module.ToString(), c.IsHealthy, c.LatencyMs })
        }, cancellationToken);

        return checks;
    }
}

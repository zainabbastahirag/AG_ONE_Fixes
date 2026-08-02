using System.Collections.Concurrent;
using AgOneSafe.Application.Assessments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgOneSafe.Infrastructure.Workers;

/// <summary>
/// Hands a full baseline scan to a background worker so the request that started it returns
/// immediately and the UI can poll progress. Verification re-scans stay inline - they touch a
/// single control and the caller needs the result to decide the finding's status.
/// </summary>
public interface IScanQueue
{
    void Enqueue(AssessmentRequest request);

    bool IsRunning(int tenantConnectionId);
}

public sealed class ScanQueue : IScanQueue
{
    private readonly ConcurrentQueue<AssessmentRequest> _queue = new();
    private readonly ConcurrentDictionary<int, byte> _running = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(AssessmentRequest request)
    {
        _running.TryAdd(request.TenantConnectionId, 0);
        _queue.Enqueue(request);
        _signal.Release();
    }

    public bool IsRunning(int tenantConnectionId) => _running.ContainsKey(tenantConnectionId);

    internal async Task<AssessmentRequest?> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        return _queue.TryDequeue(out var request) ? request : null;
    }

    internal void Complete(int tenantConnectionId) => _running.TryRemove(tenantConnectionId, out _);
}

public sealed class ScanWorker : BackgroundService
{
    private readonly IScanQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanWorker> _logger;

    public ScanWorker(IScanQueue queue, IServiceScopeFactory scopeFactory, ILogger<ScanWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = (ScanQueue)_queue;

        while (!stoppingToken.IsCancellationRequested)
        {
            AssessmentRequest? request;

            try
            {
                request = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (request is null)
            {
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var assessments = scope.ServiceProvider.GetRequiredService<IAssessmentService>();
                await assessments.RunAsync(request, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background scan for tenant {TenantId} failed", request.TenantConnectionId);
            }
            finally
            {
                queue.Complete(request.TenantConnectionId);
            }
        }
    }
}

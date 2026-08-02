using AgOneSafe.Application.Remediation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgOneSafe.Infrastructure.Workers;

/// <summary>
/// Releases remediation tasks whose off-peak maintenance window has opened. Approval is still
/// required first: scheduling moves an approved change in time, it does not bypass the gate.
/// </summary>
public sealed class MaintenanceWindowWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceWindowWorker> _logger;

    public MaintenanceWindowWorker(IServiceScopeFactory scopeFactory, ILogger<MaintenanceWindowWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var remediation = scope.ServiceProvider.GetRequiredService<IRemediationService>();

                var executed = await remediation.ExecuteDueScheduledAsync(stoppingToken);

                if (executed > 0)
                {
                    _logger.LogInformation("Maintenance window released {Count} scheduled remediation task(s)", executed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled remediation sweep failed");
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

using AgoneSentimentSales.Core.Monitoring;

namespace AgoneSentimentSales.Infrastructure.Services;

public class JobTracker : IJobTracker
{
    private static readonly AsyncLocal<JobTrackingContext?> CurrentContext = new();

    public JobTrackingContext? Current => CurrentContext.Value;

    public IDisposable BeginScope(Guid jobId)
    {
        var ctx = new JobTrackingContext { JobId = jobId };
        CurrentContext.Value = ctx;
        return new Scope(() => CurrentContext.Value = null);
    }

    public void ReportProgress(int processed, int total, string? message = null)
    {
        if (CurrentContext.Value is { } ctx)
        {
            ctx.Processed = processed;
            ctx.Total = total;
            ctx.Message = message;
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        public Scope(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}

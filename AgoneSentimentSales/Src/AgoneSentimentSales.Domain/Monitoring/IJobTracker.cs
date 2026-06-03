namespace AgoneSentimentSales.Domain.Monitoring;

public interface IJobTracker
{
    IDisposable BeginScope(Guid jobId);
    void ReportProgress(int processed, int total, string? message = null);
    JobTrackingContext? Current { get; }
}

public class JobTrackingContext
{
    public Guid JobId { get; init; }
    public int Processed { get; set; }
    public int Total { get; set; }
    public string? Message { get; set; }
}

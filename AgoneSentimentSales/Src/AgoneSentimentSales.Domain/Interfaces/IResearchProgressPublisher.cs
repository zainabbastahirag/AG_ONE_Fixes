namespace AgoneSentimentSales.Domain.Interfaces;

/// <summary>
/// Publishes real-time research job progress to connected clients (SignalR).
/// </summary>
public interface IResearchProgressPublisher
{
    Task PublishProgressAsync(
        Guid jobId,
        string phase,
        int processed,
        int total,
        string? companyName = null,
        string? message = null,
        CancellationToken cancellationToken = default);
}

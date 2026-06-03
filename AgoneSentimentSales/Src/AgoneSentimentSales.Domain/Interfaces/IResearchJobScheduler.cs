namespace AgoneSentimentSales.Domain.Interfaces;

public interface IResearchJobScheduler
{
    Task ScheduleResearchJobAsync(int companyCount, Guid jobId, CancellationToken cancellationToken = default);
    Task ScheduleDailyRefreshAsync(CancellationToken cancellationToken = default);
}

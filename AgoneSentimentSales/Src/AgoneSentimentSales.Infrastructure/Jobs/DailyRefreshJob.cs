using AgoneSentimentSales.Domain.Interfaces;
using Quartz;

namespace AgoneSentimentSales.Infrastructure.Jobs;

public class DailyRefreshJob : IJob
{
    private readonly IMarketResearchService _research;
    public DailyRefreshJob(IMarketResearchService research) => _research = research;

    public Task Execute(IJobExecutionContext context) =>
        _research.StartResearchJobAsync(100, context.CancellationToken);
}

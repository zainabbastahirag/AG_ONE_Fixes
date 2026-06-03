using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AgoneSentimentSales.Infrastructure.Services;

public class QuartzResearchJobScheduler : IResearchJobScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<QuartzResearchJobScheduler> _logger;

    public QuartzResearchJobScheduler(ISchedulerFactory schedulerFactory, ILogger<QuartzResearchJobScheduler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task ScheduleResearchJobAsync(int companyCount, Guid jobId, CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var job = JobBuilder.Create<ResearchPipelineJob>()
            .WithIdentity($"research-{jobId:N}", "sentimentsales")
            .UsingJobData(ResearchPipelineJob.CompanyCountKey, companyCount)
            .UsingJobData(ResearchPipelineJob.JobIdKey, jobId.ToString())
            .Build();
        await scheduler.ScheduleJob(job, TriggerBuilder.Create().StartNow().Build(), cancellationToken);
        _logger.LogInformation("Quartz scheduled job {JobId} for {Count} companies", jobId, companyCount);
    }

    public async Task ScheduleDailyRefreshAsync(CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.ScheduleJob(
            JobBuilder.Create<DailyRefreshJob>().WithIdentity("daily-refresh", "sentimentsales").Build(),
            TriggerBuilder.Create().WithIdentity("daily-refresh-trigger", "sentimentsales").WithCronSchedule("0 0 2 * * ?").Build(),
            cancellationToken);
    }
}

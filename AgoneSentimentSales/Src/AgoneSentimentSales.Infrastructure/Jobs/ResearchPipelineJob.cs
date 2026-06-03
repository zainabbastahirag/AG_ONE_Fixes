using AgoneSentimentSales.Domain.Interfaces;
using Quartz;

namespace AgoneSentimentSales.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class ResearchPipelineJob : IJob
{
    public const string CompanyCountKey = "CompanyCount";
    public const string JobIdKey = "JobId";
    private readonly IMarketResearchService _research;

    public ResearchPipelineJob(IMarketResearchService research) => _research = research;

    public async Task Execute(IJobExecutionContext context)
    {
        var count = context.MergedJobDataMap.GetInt(CompanyCountKey);
        var jobId = Guid.Parse(context.MergedJobDataMap.GetString(JobIdKey)!);
        await _research.ExecuteResearchPipelineAsync(jobId, count, context.CancellationToken);
    }
}

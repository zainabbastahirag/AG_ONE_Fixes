using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class JobBoardScraper : BasePublicDataScraper
{
    public JobBoardScraper(IHttpClientFactory httpClientFactory, IScraperConfigurationService configService, ILogger<JobBoardScraper> logger)
        : base(httpClientFactory, configService, logger) { }

    public override string SourceType => DataSourceTypes.JobBoard;
    public override string SourceLabel => "Job Boards";

    protected override Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, ScraperConfiguration? config, CancellationToken cancellationToken)
    {
        var url = ResolveUrl(company, config);
        IReadOnlyList<SourceExtractionEvent> events =
        [
            CreateEvent(company, jobId, config, "LeadGeneration.HiringTrends", "IT hiring velocity", url, "Glassdoor/Indeed aggregate", 0.79),
            CreateEvent(company, jobId, config, "LeadGeneration.DigitalRoles", "DevOps, data engineer openings", url, "Role taxonomy", 0.81),
            CreateEvent(company, jobId, config, "PrimaryOffshoreLocations", "India delivery hiring", url, "Location tags on posts", 0.70)
        ];
        return Task.FromResult(events);
    }
}

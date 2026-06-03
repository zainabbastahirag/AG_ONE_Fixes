using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class LinkedInScraper : BasePublicDataScraper
{
    public LinkedInScraper(IHttpClientFactory httpClientFactory, IScraperConfigurationService configService, ILogger<LinkedInScraper> logger)
        : base(httpClientFactory, configService, logger) { }

    public override string SourceType => DataSourceTypes.LinkedIn;
    public override string SourceLabel => "LinkedIn";

    protected override Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, ScraperConfiguration? config, CancellationToken cancellationToken)
    {
        var url = ResolveUrl(company, config);
        IReadOnlyList<SourceExtractionEvent> events =
        [
            CreateEvent(company, jobId, config, "ExecutiveContacts", "CIO / CTO profile", url, "Leadership page", 0.82),
            CreateEvent(company, jobId, config, "LeadGeneration.HiringTrends", "Growing digital hiring", url, "Job posts trend", 0.77),
            CreateEvent(company, jobId, config, "LeadGeneration.DigitalRoles", "Cloud & data roles open", url, "Active listings", 0.74)
        ];
        return Task.FromResult(events);
    }
}

using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class CompanyWebsiteScraper : BasePublicDataScraper
{
    public CompanyWebsiteScraper(IHttpClientFactory httpClientFactory, IScraperConfigurationService configService, ILogger<CompanyWebsiteScraper> logger)
        : base(httpClientFactory, configService, logger) { }

    public override string SourceType => DataSourceTypes.CompanyWebsite;
    public override string SourceLabel => "Corporate Website";

    protected override Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, ScraperConfiguration? config, CancellationToken cancellationToken)
    {
        var url = ResolveUrl(company, config);
        IReadOnlyList<SourceExtractionEvent> events =
        [
            CreateEvent(company, jobId, config, "TechnologyStrategy.DigitalTransformationEvidence", "Digital programme page", url, "Corporate site", 0.75),
            CreateEvent(company, jobId, config, "LeadGeneration.AsiaOperations", "Asia office locations", url, "Global footprint", 0.73),
            CreateEvent(company, jobId, config, "OutsourcingPartner.OffshoreDeliveryCenters", "Delivery center map", url, "Locations page", 0.71)
        ];
        return Task.FromResult(events);
    }
}

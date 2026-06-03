using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class PressReleaseScraper : BasePublicDataScraper
{
    public PressReleaseScraper(IHttpClientFactory httpClientFactory, IScraperConfigurationService configService, ILogger<PressReleaseScraper> logger)
        : base(httpClientFactory, configService, logger) { }

    public override string SourceType => DataSourceTypes.PressRelease;
    public override string SourceLabel => "Press Releases";

    protected override Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, ScraperConfiguration? config, CancellationToken cancellationToken)
    {
        var url = ResolveUrl(company, config);
        IReadOnlyList<SourceExtractionEvent> events =
        [
            CreateEvent(company, jobId, config, "LeadGeneration.ItAnnouncements", "Digital transformation PR", url, "Press wire", 0.84),
            CreateEvent(company, jobId, config, "OutsourcingPartner.PrimaryPartners", "Partner mentioned in release", url, "Vendor citation", 0.72),
            CreateEvent(company, jobId, config, "TechnologyStrategy.CloudStrategy", "Cloud migration announcement", url, "News snippet", 0.78)
        ];
        return Task.FromResult(events);
    }
}

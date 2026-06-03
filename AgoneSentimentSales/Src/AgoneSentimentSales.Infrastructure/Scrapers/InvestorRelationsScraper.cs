using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class InvestorRelationsScraper : BasePublicDataScraper
{
    public InvestorRelationsScraper(IHttpClientFactory httpClientFactory, IScraperConfigurationService configService, ILogger<InvestorRelationsScraper> logger)
        : base(httpClientFactory, configService, logger) { }

    public override string SourceType => DataSourceTypes.InvestorRelations;
    public override string SourceLabel => "Investor Relations";

    protected override Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, ScraperConfiguration? config, CancellationToken cancellationToken)
    {
        var url = ResolveUrl(company, config);
        IReadOnlyList<SourceExtractionEvent> events =
        [
            CreateEvent(company, jobId, config, "ItBudget.CapexGbpM", "Capex guidance", url, "IR presentation", 0.83),
            CreateEvent(company, jobId, config, "ItBudget.OpexGbpM", "Opex outlook", url, "Investor deck", 0.80)
        ];
        return Task.FromResult(events);
    }
}

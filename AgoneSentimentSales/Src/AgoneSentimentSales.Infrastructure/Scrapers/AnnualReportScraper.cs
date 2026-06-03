using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class AnnualReportScraper : BasePublicDataScraper
{
    public AnnualReportScraper(IHttpClientFactory httpClientFactory, IScraperConfigurationService configService, ILogger<AnnualReportScraper> logger)
        : base(httpClientFactory, configService, logger) { }

    public override string SourceType => DataSourceTypes.AnnualReport;
    public override string SourceLabel => "Annual Reports";

    protected override Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, ScraperConfiguration? config, CancellationToken cancellationToken)
    {
        var url = ResolveUrl(company, config);
        IReadOnlyList<SourceExtractionEvent> events =
        [
            CreateEvent(company, jobId, config, "OffshoringStatus", "Evidence from Annual Reports", url, "Public annual filing scan", 0.80),
            CreateEvent(company, jobId, config, "ItBudget.EstimatedItBudgetGbpM", "IT spend disclosure", url, "Capex/Opex narrative", 0.76),
            CreateEvent(company, jobId, config, "TechnologyStrategy.KeyTechInitiatives", "Technology programme", url, "Strategy section", 0.78)
        ];
        return Task.FromResult(events);
    }
}

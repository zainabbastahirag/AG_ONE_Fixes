using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class PressReleaseScraper : BasePublicDataScraper
{
    public PressReleaseScraper(IHttpClientFactory httpClientFactory, ILogger<PressReleaseScraper> logger)
        : base(httpClientFactory, logger) { }

    public override string SourceType => DataSourceTypes.PressRelease;
    public override string SourceLabel => "Press Releases";

    protected override Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, CancellationToken cancellationToken)
    {
        var ticker = company.Ticker.ToLowerInvariant();
        var url = BuildUrl(company, ticker);
        var events = new List<SourceExtractionEvent>
        {
            CreateEvent(company, jobId, "OffshoringStatus", "Evidence from Press Releases", url, "Public Press Releases scan", 0.80),
            CreateEvent(company, jobId, "LeadGeneration.ItAnnouncements", "Transformation signal", url, "Recent disclosure", 0.75),
            CreateEvent(company, jobId, "TechnologyStrategy.KeyTechInitiatives", "Tech programme mention", url, "Strategy content", 0.78)
        };
        return Task.FromResult<IReadOnlyList<SourceExtractionEvent>>(events);
    }

    private string BuildUrl(LseCompany company, string ticker) => SourceType switch
    {
        DataSourceTypes.AnnualReport => $"https://www.londonstockexchange.com/stock/{company.Ticker}/",
        DataSourceTypes.LinkedIn => $"https://www.linkedin.com/company/{ticker}",
        DataSourceTypes.JobBoard => $"https://www.glassdoor.co.uk/Search/results.htm?keyword={ticker}",
        DataSourceTypes.PressRelease => $"https://www.google.com/search?q={ticker}+press+release+digital",
        _ => $"https://www.{ticker}.com"
    };
}

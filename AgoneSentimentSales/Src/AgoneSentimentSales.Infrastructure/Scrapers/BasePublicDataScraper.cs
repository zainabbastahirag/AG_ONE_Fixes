using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public abstract class BasePublicDataScraper : IDataSourceScraper
{
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly IScraperConfigurationService ConfigService;
    protected readonly ILogger Logger;

    protected BasePublicDataScraper(
        IHttpClientFactory httpClientFactory,
        IScraperConfigurationService configService,
        ILogger logger)
    {
        HttpClientFactory = httpClientFactory;
        ConfigService = configService;
        Logger = logger;
    }

    public abstract string SourceType { get; }
    public abstract string SourceLabel { get; }

    public async Task<IReadOnlyList<SourceExtractionEvent>> ScrapeAsync(
        LseCompany company, Guid jobId, CancellationToken cancellationToken = default)
    {
        var config = await ConfigService.GetBySourceTypeAsync(SourceType, cancellationToken);
        if (config is { IsEnabled: false })
        {
            Logger.LogInformation("Scraper {Source} disabled in configuration", SourceType);
            return [];
        }

        var label = config?.DisplayName ?? SourceLabel;
        Logger.LogInformation("Scraping {Source} for {Company} (max {Max} items)", label, company.CompanyName, config?.MaxItemsToScrape ?? 10);

        var delayMin = config?.DelayMsMin ?? 80;
        var delayMax = config?.DelayMsMax ?? 220;
        await Task.Delay(Random.Shared.Next(delayMin, delayMax + 1), cancellationToken);

        var facts = await ExtractFactsAsync(company, jobId, config, cancellationToken);
        var max = config?.MaxItemsToScrape ?? facts.Count;
        return facts.Take(Math.Max(1, max)).ToList();
    }

    protected abstract Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, ScraperConfiguration? config, CancellationToken cancellationToken);

    protected string ResolveUrl(LseCompany company, ScraperConfiguration? config)
    {
        var template = config?.BaseUrlTemplate ?? "https://www.londonstockexchange.com/stock/{ticker}/";
        return template
            .Replace("{ticker}", company.Ticker, StringComparison.OrdinalIgnoreCase)
            .Replace("{company}", Uri.EscapeDataString(company.CompanyName), StringComparison.OrdinalIgnoreCase);
    }

    protected SourceExtractionEvent CreateEvent(
        LseCompany company, Guid jobId, ScraperConfiguration? config,
        string field, string value, string? urlOverride, string snippet, double confidence)
    {
        var url = urlOverride ?? ResolveUrl(company, config);
        return new SourceExtractionEvent
        {
            ResearchJobId = jobId,
            LseCompanyId = company.Id > 0 ? company.Id : null,
            CompanyName = company.CompanyName,
            SourceType = SourceType,
            SourceLabel = config?.DisplayName ?? SourceLabel,
            SourceUrl = url,
            FieldName = field,
            ExtractedValue = value,
            RawSnippet = snippet,
            ConfidenceScore = confidence
        };
    }
}

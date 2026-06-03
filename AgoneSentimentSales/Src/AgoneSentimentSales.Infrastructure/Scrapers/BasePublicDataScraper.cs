
using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public abstract class BasePublicDataScraper : IDataSourceScraper
{
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly ILogger Logger;

    protected BasePublicDataScraper(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        HttpClientFactory = httpClientFactory;
        Logger = logger;
    }

    public abstract string SourceType { get; }
    public abstract string SourceLabel { get; }

    public async Task<IReadOnlyList<SourceExtractionEvent>> ScrapeAsync(
        LseCompany company, Guid jobId, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Scraping {Source} for {Company}", SourceLabel, company.CompanyName);
        await Task.Delay(Random.Shared.Next(80, 220), cancellationToken);
        return await ExtractFactsAsync(company, jobId, cancellationToken);
    }

    protected abstract Task<IReadOnlyList<SourceExtractionEvent>> ExtractFactsAsync(
        LseCompany company, Guid jobId, CancellationToken cancellationToken);

    protected SourceExtractionEvent CreateEvent(
        LseCompany company, Guid jobId, string field, string value, string url, string snippet, double confidence) =>
        new()
        {
            ResearchJobId = jobId,
            LseCompanyId = company.Id > 0 ? company.Id : null,
            CompanyName = company.CompanyName,
            SourceType = SourceType,
            SourceLabel = SourceLabel,
            SourceUrl = url,
            FieldName = field,
            ExtractedValue = value,
            RawSnippet = snippet,
            ConfidenceScore = confidence
        };
}

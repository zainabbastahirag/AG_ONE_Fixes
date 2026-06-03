using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class ScraperOrchestrator : IScraperOrchestrator
{
    private readonly IEnumerable<IDataSourceScraper> _scrapers;
    private readonly IExtractionEventPublisher _publisher;
    private readonly ILogger<ScraperOrchestrator> _logger;

    public ScraperOrchestrator(
        IEnumerable<IDataSourceScraper> scrapers,
        IExtractionEventPublisher publisher,
        ILogger<ScraperOrchestrator> logger)
    {
        _scrapers = scrapers;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SourceExtractionEvent>> RunAllSourcesAsync(
        LseCompany company, Guid jobId, CancellationToken cancellationToken = default)
    {
        var all = new List<SourceExtractionEvent>();
        foreach (var scraper in _scrapers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = await scraper.ScrapeAsync(company, jobId, cancellationToken);
            foreach (var ev in batch)
            {
                all.Add(ev);
                await _publisher.PublishAsync(ev, cancellationToken);
            }
            _logger.LogInformation("Source {Source}: {Count} facts for {Company}",
                scraper.SourceLabel, batch.Count, company.CompanyName);
        }
        return all;
    }
}

using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgoneSentimentSales.Infrastructure.Scrapers;

public class ScraperOrchestrator : IScraperOrchestrator
{
    private readonly IEnumerable<IDataSourceScraper> _scrapers;
    private readonly IScraperConfigurationService _configService;
    private readonly IExtractionEventPublisher _publisher;
    private readonly ILogger<ScraperOrchestrator> _logger;

    public ScraperOrchestrator(
        IEnumerable<IDataSourceScraper> scrapers,
        IScraperConfigurationService configService,
        IExtractionEventPublisher publisher,
        ILogger<ScraperOrchestrator> logger)
    {
        _scrapers = scrapers;
        _configService = configService;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SourceExtractionEvent>> RunAllSourcesAsync(
        LseCompany company, Guid jobId, CancellationToken cancellationToken = default)
    {
        var configs = await _configService.GetEnabledAsync(cancellationToken);
        var configByType = configs.ToDictionary(c => c.SourceType, StringComparer.OrdinalIgnoreCase);
        var all = new List<SourceExtractionEvent>();

        foreach (var scraper in _scrapers.OrderBy(s => configByType.GetValueOrDefault(s.SourceType)?.Priority ?? 99))
        {
            if (configByType.TryGetValue(scraper.SourceType, out var cfg) && !cfg.IsEnabled)
                continue;

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

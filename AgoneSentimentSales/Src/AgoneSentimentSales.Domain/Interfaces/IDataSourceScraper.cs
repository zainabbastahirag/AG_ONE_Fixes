
using AgoneSentimentSales.Domain.Entities;

namespace AgoneSentimentSales.Domain.Interfaces;

public interface IDataSourceScraper
{
    string SourceType { get; }
    string SourceLabel { get; }
    Task<IReadOnlyList<SourceExtractionEvent>> ScrapeAsync(
        LseCompany company,
        Guid jobId,
        CancellationToken cancellationToken = default);
}

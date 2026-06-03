
using AgoneSentimentSales.Domain.Entities;

namespace AgoneSentimentSales.Domain.Interfaces;

public interface IScraperOrchestrator
{
    Task<IReadOnlyList<SourceExtractionEvent>> RunAllSourcesAsync(
        LseCompany company,
        Guid jobId,
        CancellationToken cancellationToken = default);
}

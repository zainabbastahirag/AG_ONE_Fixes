
using AgoneSentimentSales.Domain.Entities;

namespace AgoneSentimentSales.Domain.Interfaces;

public interface IExtractionEventPublisher
{
    Task PublishAsync(SourceExtractionEvent extractionEvent, CancellationToken cancellationToken = default);
    Task PublishBatchAsync(IEnumerable<SourceExtractionEvent> events, CancellationToken cancellationToken = default);
}

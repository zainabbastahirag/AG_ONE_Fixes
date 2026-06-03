using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;

namespace AgoneSentimentSales.API.Services;

public class CompositeExtractionEventPublisher : IExtractionEventPublisher
{
    private readonly IEnumerable<IExtractionEventPublisher> _publishers;
    public CompositeExtractionEventPublisher(IEnumerable<IExtractionEventPublisher> publishers) => _publishers = publishers;

    public async Task PublishAsync(SourceExtractionEvent extractionEvent, CancellationToken cancellationToken = default)
    {
        foreach (var p in _publishers) await p.PublishAsync(extractionEvent, cancellationToken);
    }

    public async Task PublishBatchAsync(IEnumerable<SourceExtractionEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var e in events) await PublishAsync(e, cancellationToken);
    }
}

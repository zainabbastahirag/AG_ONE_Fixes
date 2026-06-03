using AgoneSentimentSales.API.Hubs;
using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace AgoneSentimentSales.API.Services;

public class SignalRExtractionEventPublisher : IExtractionEventPublisher
{
    private readonly IHubContext<ExtractionHub> _hub;
    public SignalRExtractionEventPublisher(IHubContext<ExtractionHub> hub) => _hub = hub;

    public Task PublishAsync(SourceExtractionEvent e, CancellationToken cancellationToken = default) =>
        PublishBatchAsync([e], cancellationToken);

    public async Task PublishBatchAsync(IEnumerable<SourceExtractionEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var e in events)
        {
            var dto = new ExtractionEventDto(e.Id, e.ResearchJobId, e.CompanyName, e.SourceType, e.SourceLabel,
                e.SourceUrl, e.FieldName, e.ExtractedValue, e.RawSnippet, e.ConfidenceScore, e.ExtractedAt);
            await _hub.Clients.Group(e.ResearchJobId.ToString()).SendAsync("ExtractionReceived", dto, cancellationToken);
        }
    }
}

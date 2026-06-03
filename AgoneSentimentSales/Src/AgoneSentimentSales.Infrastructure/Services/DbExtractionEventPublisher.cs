using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Infrastructure.Data;

namespace AgoneSentimentSales.Infrastructure.Services;

public class DbExtractionEventPublisher : IExtractionEventPublisher
{
    private readonly SentimentSalesDbContext _db;

    public DbExtractionEventPublisher(SentimentSalesDbContext db) => _db = db;

    public async Task PublishAsync(SourceExtractionEvent extractionEvent, CancellationToken cancellationToken = default)
    {
        _db.SourceExtractionEvents.Add(extractionEvent);
        if (extractionEvent.LseCompanyId is > 0)
        {
            _db.SourcedDataPoints.Add(new SourcedDataPoint
            {
                LseCompanyId = extractionEvent.LseCompanyId.Value,
                EntityName = "LseCompany",
                FieldName = extractionEvent.FieldName,
                FieldValue = extractionEvent.ExtractedValue,
                SourceType = extractionEvent.SourceType,
                SourceUrl = extractionEvent.SourceUrl,
                ConfidenceScore = extractionEvent.ConfidenceScore
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task PublishBatchAsync(IEnumerable<SourceExtractionEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var e in events)
            await PublishAsync(e, cancellationToken);
    }
}

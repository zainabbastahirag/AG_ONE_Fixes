
namespace AgoneSentimentSales.Domain.Entities;

/// <summary>Persisted attribution: which field value came from which public source.</summary>
public class SourcedDataPoint
{
    public int Id { get; set; }
    public int LseCompanyId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string FieldValue { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public LseCompany? Company { get; set; }
}

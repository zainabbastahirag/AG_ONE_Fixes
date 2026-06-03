
namespace AgoneSentimentSales.Domain.Entities;

public class SourceExtractionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResearchJobId { get; set; }
    public int? LseCompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string ExtractedValue { get; set; } = string.Empty;
    public string? RawSnippet { get; set; }
    public double ConfidenceScore { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    public LseCompany? Company { get; set; }
    public ResearchJob? ResearchJob { get; set; }
}

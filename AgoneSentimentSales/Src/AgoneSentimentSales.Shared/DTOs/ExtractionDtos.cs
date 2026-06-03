
namespace AgoneSentimentSales.Shared.DTOs;

public record ExtractionEventDto(
    Guid EventId,
    Guid JobId,
    string CompanyName,
    string SourceType,
    string SourceLabel,
    string SourceUrl,
    string FieldName,
    string ExtractedValue,
    string? RawSnippet,
    double ConfidenceScore,
    DateTime ExtractedAt);

public record SourceSummaryDto(string SourceType, string SourceLabel, int FactCount, double AvgConfidence);

public record CompanySourceBreakdownDto(
    int CompanyId,
    string CompanyName,
    IReadOnlyList<SourceSummaryDto> Sources,
    IReadOnlyList<ExtractionEventDto> RecentEvents);

public record LiveExtractionFeedDto(
    Guid JobId,
    string Status,
    int TotalEvents,
    IReadOnlyList<ExtractionEventDto> Events,
    IReadOnlyDictionary<string, int> CountBySource);

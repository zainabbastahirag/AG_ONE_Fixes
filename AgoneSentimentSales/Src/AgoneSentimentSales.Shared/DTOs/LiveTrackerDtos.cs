namespace AgoneSentimentSales.Shared.DTOs;

public record LiveTrackerDto(
    DashboardSummaryDto Dashboard,
    IReadOnlyDictionary<string, int> OffshoringCounts,
    IReadOnlyDictionary<string, int> SourceCounts,
    ResearchJobResponse? LatestJob,
    IReadOnlyList<string> ExcelSheets);

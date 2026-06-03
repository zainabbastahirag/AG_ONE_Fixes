namespace AgoneSentimentSales.Shared.DTOs;

public record ScraperConfigurationDto(
    int Id,
    string SourceType,
    string DisplayName,
    string BaseUrlTemplate,
    bool IsEnabled,
    int MaxItemsToScrape,
    int DelayMsMin,
    int DelayMsMax,
    int Priority,
    string? Notes,
    DateTime UpdatedAt);

public record UpsertScraperConfigurationRequest(
    string SourceType,
    string DisplayName,
    string BaseUrlTemplate,
    bool IsEnabled,
    int MaxItemsToScrape,
    int DelayMsMin,
    int DelayMsMax,
    int Priority,
    string? Notes);

public record ReportViewMetaDto(string ViewId, string Title, string Description, string Icon, string ExcelSheetName);

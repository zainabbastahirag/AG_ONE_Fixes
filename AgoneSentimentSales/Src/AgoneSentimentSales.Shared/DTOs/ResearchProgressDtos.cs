namespace AgoneSentimentSales.Shared.DTOs;

public record ResearchProgressDto(
    Guid JobId,
    string Phase,
    int Processed,
    int Total,
    string? CompanyName,
    string? Message,
    DateTime TimestampUtc);

public static class ResearchPhases
{
    public const string Initializing = "Initializing";
    public const string LoadingCompanies = "LoadingCompanies";
    public const string AgentEnrichment = "AgentEnrichment";
    public const string PublicSourceScraping = "PublicSourceScraping";
    public const string Persisting = "Persisting";
    public const string ExcelExport = "ExcelExport";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

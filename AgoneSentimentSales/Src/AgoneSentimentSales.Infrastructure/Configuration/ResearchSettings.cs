namespace AgoneSentimentSales.Infrastructure.Configuration;

public class ResearchSettings
{
    public const string SectionName = "AgoneSentimentSales";
    public int DefaultTopCompanyCount { get; set; } = 100;
    public string ExportDirectory { get; set; } = "exports";
    public string ResearchFocusRegions { get; set; } = "India,Asia";
}

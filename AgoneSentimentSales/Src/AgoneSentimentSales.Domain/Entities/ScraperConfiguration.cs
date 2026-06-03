namespace AgoneSentimentSales.Domain.Entities;

public class ScraperConfiguration
{
    public int Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseUrlTemplate { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int MaxItemsToScrape { get; set; } = 10;
    public int DelayMsMin { get; set; } = 80;
    public int DelayMsMax { get; set; } = 220;
    public int Priority { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

using AgoneSentimentSales.Core.Enums;

namespace AgoneSentimentSales.Core.Entities;

public class ResearchJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ResearchJobStatus Status { get; set; } = ResearchJobStatus.Pending;
    public int TargetCompanyCount { get; set; } = 100;
    public int ProcessedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputFilePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
}

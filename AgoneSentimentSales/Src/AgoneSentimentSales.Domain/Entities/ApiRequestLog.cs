namespace AgoneSentimentSales.Domain.Entities;

public class ApiRequestLog
{
    public long Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? ClientIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

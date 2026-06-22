namespace AGONECompliance.Shared.Models;

public class JobActivity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string JobType { get; set; } = "";
    public string Step { get; set; } = "";
    public string Status { get; set; } = "Running";
    public string? Message { get; set; }
    public string? ErrorDetail { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int? DurationMs { get; set; }
}

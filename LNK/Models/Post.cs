namespace LNK.Models;

public class Post
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Hook { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string CallToAction { get; set; } = string.Empty;
    public string Hashtags { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string FullText { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EmailedAt { get; set; }
    public DateTime? ScheduledFor { get; set; }

    public ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();
}

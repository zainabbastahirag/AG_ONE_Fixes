namespace LNK.Models;

public class EmailLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? PostId { get; set; }
    public Post? Post { get; set; }

    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

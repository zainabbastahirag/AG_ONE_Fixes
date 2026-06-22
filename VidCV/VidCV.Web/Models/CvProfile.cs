namespace VidCV.Web.Models;

public class CvProfile
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? JobTitle { get; set; }
    public string? Summary { get; set; }
    public string? Skills { get; set; }
    public string? Experience { get; set; }
    public string? Education { get; set; }
    public string? CvFileName { get; set; }
    public string? CvFilePath { get; set; }
    public string? VideoScript { get; set; }
    public string? VideoPath { get; set; }
    public string? VideoUrl { get; set; }
    public VideoStatus Status { get; set; } = VideoStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum VideoStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

namespace VidCV.Web.Models;

public class VideoTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string BackgroundColor { get; set; } = "#1E3A5F";
    public string AccentColor { get; set; } = "#3B82F6";
    public string TextColor { get; set; } = "#FFFFFF";
    public string FontFamily { get; set; } = "Arial";
    public int DurationSeconds { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

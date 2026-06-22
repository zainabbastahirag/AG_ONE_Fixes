namespace LNK.Models;

public class UserSettings
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Industry { get; set; } = "Technology";
    public string Topics { get; set; } = "Leadership, Innovation";
    public string Keywords { get; set; } = "";
    public string Tone { get; set; } = "Professional";
    public string PostLength { get; set; } = "Medium";
    public TimeSpan DailyPostTime { get; set; } = new(9, 0, 0);
    public string Timezone { get; set; } = "UTC";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

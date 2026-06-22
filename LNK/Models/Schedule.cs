namespace LNK.Models;

public class Schedule
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public TimeSpan PostTime { get; set; } = new(9, 0, 0);
    public string Timezone { get; set; } = "UTC";
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
}

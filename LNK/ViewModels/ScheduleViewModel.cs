using LNK.Models;

namespace LNK.ViewModels;

public class ScheduleViewModel
{
    public Schedule Schedule { get; set; } = null!;
    public UserSettings Settings { get; set; } = null!;
    public List<UpcomingSlotViewModel> UpcomingSlots { get; set; } = new();
    public List<Post> ScheduledPosts { get; set; } = new();
    public List<EmailLog> RecentEmails { get; set; } = new();
}

public class UpcomingSlotViewModel
{
    public DateTime DateTime { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public string TimeLabel { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool HasPost { get; set; }
    public int? PostId { get; set; }
    public string Status { get; set; } = "Scheduled";
}

public class ScheduleEditViewModel
{
    public TimeSpan PostTime { get; set; } = new(9, 0, 0);
    public string Timezone { get; set; } = "UTC";
    public bool IsActive { get; set; } = true;
}

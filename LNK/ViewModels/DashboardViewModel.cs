using LNK.Models;

namespace LNK.ViewModels;

public class DashboardViewModel
{
    public Post? TodaysPost { get; set; }
    public DateTime? NextScheduled { get; set; }
    public int PostsThisMonth { get; set; }
    public int EmailsSent { get; set; }
    public int EmailsFailed { get; set; }
    public List<Post> RecentPosts { get; set; } = new();
    public UserSettings? Settings { get; set; }
    public Schedule? Schedule { get; set; }
    public List<UpcomingSlotViewModel> UpcomingSlots { get; set; } = new();
    public List<EmailLog> RecentEmails { get; set; } = new();
    public int[] WeeklyPosts { get; set; } = [0, 0, 0, 0, 0, 0, 0];
    public string DisplayName { get; set; } = string.Empty;
}

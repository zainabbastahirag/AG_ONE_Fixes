using LNK.Models;
using LNK.ViewModels;

namespace LNK.Helpers;

public static class ScheduleHelper
{
    public static List<UpcomingSlotViewModel> BuildUpcomingSlots(
        UserSettings settings,
        Schedule? schedule,
        IReadOnlyList<Post> posts,
        int days = 7)
    {
        if (schedule != null && !schedule.IsActive)
            return [];

        var slots = new List<UpcomingSlotViewModel>();
        var today = DateTime.UtcNow.Date;

        for (var i = 0; i < days; i++)
        {
            var date = today.AddDays(i);
            var slotTime = date.Add(settings.DailyPostTime);
            if (i == 0 && slotTime < DateTime.UtcNow)
                continue;

            var post = posts
                .Where(p => p.ScheduledFor?.Date == date || (i == 0 && p.GeneratedAt.Date == date))
                .OrderByDescending(p => p.GeneratedAt)
                .FirstOrDefault();

            slots.Add(new UpcomingSlotViewModel
            {
                DateTime = slotTime,
                DayLabel = i == 0 ? "Today" : i == 1 ? "Tomorrow" : slotTime.ToString("ddd, MMM d"),
                TimeLabel = slotTime.ToString("h:mm tt"),
                IsToday = i == 0,
                HasPost = post != null,
                PostId = post?.Id,
                Status = post?.Status ?? "Scheduled"
            });
        }

        if (schedule?.NextRunAt != null && !slots.Any(s => s.DateTime.Date == schedule.NextRunAt.Value.Date))
        {
            slots.Insert(0, new UpcomingSlotViewModel
            {
                DateTime = schedule.NextRunAt.Value,
                DayLabel = "Next run",
                TimeLabel = schedule.NextRunAt.Value.ToString("h:mm tt"),
                IsToday = schedule.NextRunAt.Value.Date == today,
                HasPost = false,
                Status = "Queued"
            });
        }

        return slots.OrderBy(s => s.DateTime).Take(days + 1).ToList();
    }

    public static int[] BuildWeeklyChart(IReadOnlyList<Post> posts)
    {
        var result = new int[7];
        var start = DateTime.UtcNow.Date.AddDays(-6);
        for (var i = 0; i < 7; i++)
        {
            var day = start.AddDays(i);
            result[i] = posts.Count(p => p.GeneratedAt.Date == day);
        }
        return result;
    }
}

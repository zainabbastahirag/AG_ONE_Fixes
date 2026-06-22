using LNK.Data;
using LNK.Helpers;
using LNK.Models;
using LNK.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LNK.Controllers;

[Authorize]
public class ScheduleController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ScheduleController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        if (!user.OnboardingCompleted) return RedirectToAction("Index", "Onboarding");

        var settings = await _db.UserSettings.FirstAsync(s => s.UserId == user.Id);
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.UserId == user.Id);
        var posts = await _db.Posts.Where(p => p.UserId == user.Id).OrderByDescending(p => p.GeneratedAt).Take(30).ToListAsync();
        var emails = await _db.EmailLogs.Where(e => e.UserId == user.Id).OrderByDescending(e => e.SentAt).Take(8).ToListAsync();

        if (schedule == null)
        {
            schedule = new Schedule { UserId = user.Id, PostTime = settings.DailyPostTime, IsActive = true };
            _db.Schedules.Add(schedule);
            await _db.SaveChangesAsync();
        }

        return View(new ScheduleViewModel
        {
            Schedule = schedule,
            Settings = settings,
            UpcomingSlots = ScheduleHelper.BuildUpcomingSlots(settings, schedule, posts, 14),
            ScheduledPosts = posts.Where(p => p.ScheduledFor != null).OrderBy(p => p.ScheduledFor).Take(10).ToList(),
            RecentEmails = emails
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(ScheduleEditViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var settings = await _db.UserSettings.FirstAsync(s => s.UserId == user.Id);
        settings.DailyPostTime = model.PostTime;
        settings.Timezone = model.Timezone;
        settings.UpdatedAt = DateTime.UtcNow;

        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (schedule == null)
        {
            schedule = new Schedule { UserId = user.Id };
            _db.Schedules.Add(schedule);
        }

        schedule.PostTime = model.PostTime;
        schedule.Timezone = model.Timezone;
        schedule.IsActive = model.IsActive;
        schedule.NextRunAt = ComputeNextRun(model.PostTime, model.IsActive);

        await _db.SaveChangesAsync();
        TempData["Toast"] = model.IsActive
            ? $"Schedule saved — next post at {schedule.NextRunAt:h:mm tt}"
            : "Schedule paused. Enable when you're ready.";
        return RedirectToAction(nameof(Index));
    }

    private static DateTime? ComputeNextRun(TimeSpan postTime, bool isActive)
    {
        if (!isActive) return null;
        var next = DateTime.UtcNow.Date.Add(postTime);
        if (next <= DateTime.UtcNow) next = next.AddDays(1);
        return next;
    }
}

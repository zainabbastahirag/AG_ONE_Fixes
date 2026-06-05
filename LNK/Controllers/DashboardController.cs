using LNK.Data;
using LNK.Helpers;
using LNK.Models;
using LNK.Services;
using LNK.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LNK.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPostGenerationService _generator;

    public DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPostGenerationService generator)
    {
        _db = db;
        _userManager = userManager;
        _generator = generator;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        if (!user.OnboardingCompleted) return RedirectToAction("Index", "Onboarding");

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.UserId == user.Id);
        var allPosts = await _db.Posts.Where(p => p.UserId == user.Id).OrderByDescending(p => p.GeneratedAt).ToListAsync();
        var monthPosts = allPosts.Where(p => p.GeneratedAt >= monthStart).ToList();

        var vm = new DashboardViewModel
        {
            DisplayName = user.DisplayName ?? user.Email ?? "there",
            TodaysPost = allPosts.FirstOrDefault(p => p.GeneratedAt.Date == DateTime.UtcNow.Date),
            NextScheduled = schedule?.NextRunAt ?? DateTime.UtcNow.Date.AddDays(1).Add(settings?.DailyPostTime ?? TimeSpan.FromHours(9)),
            PostsThisMonth = monthPosts.Count,
            EmailsSent = await _db.EmailLogs.CountAsync(e => e.UserId == user.Id && e.Success && e.SentAt >= monthStart),
            EmailsFailed = await _db.EmailLogs.CountAsync(e => e.UserId == user.Id && !e.Success && e.SentAt >= monthStart),
            RecentPosts = allPosts.Take(8).ToList(),
            Settings = settings,
            Schedule = schedule,
            UpcomingSlots = settings != null ? ScheduleHelper.BuildUpcomingSlots(settings, schedule, allPosts, 5) : [],
            RecentEmails = await _db.EmailLogs.Where(e => e.UserId == user.Id).OrderByDescending(e => e.SentAt).Take(5).ToListAsync(),
            WeeklyPosts = ScheduleHelper.BuildWeeklyChart(allPosts)
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickGenerate()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (settings == null) return RedirectToAction("Index", "Onboarding");

        var post = await _generator.GenerateForUserAsync(user, settings);
        TempData["Toast"] = "Fresh post generated — review and publish when ready.";
        return RedirectToAction("Review", "Posts", new { id = post.Id });
    }
}

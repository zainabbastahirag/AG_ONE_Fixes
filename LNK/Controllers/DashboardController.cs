using LNK.Data;
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
        var vm = new DashboardViewModel
        {
            TodaysPost = await _db.Posts.Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.GeneratedAt)
                .FirstOrDefaultAsync(p => p.GeneratedAt.Date == DateTime.UtcNow.Date),
            NextScheduled = (await _db.Schedules.FirstOrDefaultAsync(s => s.UserId == user.Id))?.NextRunAt
                ?? DateTime.UtcNow.Date.AddDays(1).Add((await _db.UserSettings.FirstAsync(s => s.UserId == user.Id)).DailyPostTime),
            PostsThisMonth = await _db.Posts.CountAsync(p => p.UserId == user.Id && p.GeneratedAt >= monthStart),
            EmailsSent = await _db.EmailLogs.CountAsync(e => e.UserId == user.Id && e.Success && e.SentAt >= monthStart),
            RecentPosts = await _db.Posts.Where(p => p.UserId == user.Id).OrderByDescending(p => p.GeneratedAt).Take(6).ToListAsync(),
            Settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == user.Id)
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
        return RedirectToAction("Review", "Posts", new { id = post.Id });
    }
}

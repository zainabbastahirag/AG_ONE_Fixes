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
public class OnboardingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public OnboardingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.OnboardingCompleted == true) return RedirectToAction("Index", "Dashboard");

        ViewBag.Tones = PostTone.All;
        ViewBag.Lengths = PostTone.PostLengths;
        return View(new OnboardingViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(OnboardingViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Tones = PostTone.All;
            ViewBag.Lengths = PostTone.PostLengths;
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (settings == null)
        {
            settings = new UserSettings { UserId = user.Id };
            _db.UserSettings.Add(settings);
        }

        settings.Industry = model.Industry;
        settings.Topics = model.Topics;
        settings.Keywords = model.Keywords;
        settings.Tone = model.Tone;
        settings.PostLength = model.PostLength;
        settings.DailyPostTime = model.DailyPostTime;
        settings.Timezone = model.Timezone;
        settings.UpdatedAt = DateTime.UtcNow;

        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (schedule == null)
        {
            schedule = new Schedule { UserId = user.Id };
            _db.Schedules.Add(schedule);
        }
        schedule.PostTime = model.DailyPostTime;
        schedule.Timezone = model.Timezone;
        schedule.IsActive = true;
        schedule.NextRunAt = DateTime.UtcNow.Date.AddDays(1).Add(model.DailyPostTime);

        user.OnboardingCompleted = true;
        await _userManager.UpdateAsync(user);
        await _db.SaveChangesAsync();

        return RedirectToAction("Index", "Dashboard");
    }
}

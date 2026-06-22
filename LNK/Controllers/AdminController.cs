using LNK.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LNK.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;

    public AdminController(ApplicationDbContext db) => _db = db;

    public IActionResult Index() => View();

    public async Task<IActionResult> Users() =>
        View(await _db.Users.OrderByDescending(u => u.CreatedAt).Take(100).ToListAsync());

    public async Task<IActionResult> Posts() =>
        View(await _db.Posts.Include(p => p.User).OrderByDescending(p => p.GeneratedAt).Take(100).ToListAsync());

    public async Task<IActionResult> EmailLogs() =>
        View(await _db.EmailLogs.OrderByDescending(e => e.SentAt).Take(100).ToListAsync());

    public async Task<IActionResult> Settings()
    {
        ViewBag.Settings = await _db.Settings.OrderBy(s => s.Key).ToListAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSetting(string key, string value)
    {
        var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key);
        if (s != null)
        {
            s.Value = value;
            s.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Settings));
    }
}

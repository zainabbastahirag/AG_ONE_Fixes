using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;

namespace TeamPulse.Controllers;

[Authorize]
public class ReleasesController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReleasesController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var releases = await _db.Releases
            .Include(r => r.Team)
            .OrderBy(r => r.Status)
            .ThenBy(r => r.TargetDate)
            .ToListAsync();
        return View(releases);
    }

    public async Task<IActionResult> Details(int id)
    {
        var release = await _db.Releases.Include(r => r.Team).FirstOrDefaultAsync(r => r.Id == id);
        if (release == null) return NotFound();
        return View(release);
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create()
    {
        await Populate();
        return View(new Release());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create(Release release)
    {
        if (!ModelState.IsValid)
        {
            await Populate(release.TeamId);
            return View(release);
        }
        if (release.Status == ReleaseStatus.Released)
        {
            release.ProgressPercent = 100;
            release.ReleasedDate ??= DateTime.UtcNow.Date;
        }
        _db.Releases.Add(release);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Release \"{release.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id)
    {
        var release = await _db.Releases.FindAsync(id);
        if (release == null) return NotFound();
        await Populate(release.TeamId);
        return View(release);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id, Release release)
    {
        if (id != release.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            await Populate(release.TeamId);
            return View(release);
        }
        var existing = await _db.Releases.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Name = release.Name;
        existing.Version = release.Version;
        existing.TeamId = release.TeamId;
        existing.Status = release.Status;
        existing.TargetDate = release.TargetDate;
        existing.ReleasedDate = release.ReleasedDate;
        existing.ProgressPercent = release.ProgressPercent;
        existing.Notes = release.Notes;
        if (release.Status == ReleaseStatus.Released)
        {
            existing.ProgressPercent = 100;
            existing.ReleasedDate ??= DateTime.UtcNow.Date;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Release \"{release.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Delete(int id)
    {
        var release = await _db.Releases.Include(r => r.Team).FirstOrDefaultAsync(r => r.Id == id);
        if (release == null) return NotFound();
        return View(release);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var release = await _db.Releases.FindAsync(id);
        if (release != null)
        {
            _db.Releases.Remove(release);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Release \"{release.Name}\" deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task Populate(int? team = null)
    {
        ViewBag.Teams = new SelectList(await _db.Teams.OrderBy(t => t.Name).ToListAsync(), "Id", "Name", team);
    }
}

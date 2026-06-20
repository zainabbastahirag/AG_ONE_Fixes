using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;

namespace TeamPulse.Controllers;

[Authorize]
public class SprintsController : Controller
{
    private readonly ApplicationDbContext _db;

    public SprintsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var sprints = await _db.Sprints
            .Include(s => s.WorkItems)
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Number)
            .ToListAsync();
        return View(sprints);
    }

    public async Task<IActionResult> Details(int id)
    {
        var sprint = await _db.Sprints
            .Include(s => s.WorkItems).ThenInclude(w => w.Team)
            .Include(s => s.WorkItems).ThenInclude(w => w.AssignedMember)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sprint == null) return NotFound();
        return View(sprint);
    }

    [Authorize(Roles = "Admin,TechLead")]
    public IActionResult Create() => View(new Sprint());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create(Sprint sprint)
    {
        if (!ModelState.IsValid) return View(sprint);
        if (sprint.IsActive)
            await _db.Sprints.ForEachAsync(s => s.IsActive = false);
        _db.Sprints.Add(sprint);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{sprint.Name} created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id)
    {
        var sprint = await _db.Sprints.FindAsync(id);
        if (sprint == null) return NotFound();
        return View(sprint);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id, Sprint sprint)
    {
        if (id != sprint.Id) return NotFound();
        if (!ModelState.IsValid) return View(sprint);
        if (sprint.IsActive)
            await _db.Sprints.Where(s => s.Id != id).ForEachAsync(s => s.IsActive = false);
        var existing = await _db.Sprints.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Name = sprint.Name;
        existing.Number = sprint.Number;
        existing.Quarter = sprint.Quarter;
        existing.Year = sprint.Year;
        existing.StartDate = sprint.StartDate;
        existing.EndDate = sprint.EndDate;
        existing.Goal = sprint.Goal;
        existing.IsActive = sprint.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{sprint.Name} updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var sprint = await _db.Sprints.FindAsync(id);
        if (sprint == null) return NotFound();
        return View(sprint);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var sprint = await _db.Sprints.FindAsync(id);
        if (sprint != null)
        {
            _db.Sprints.Remove(sprint);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{sprint.Name} deleted.";
        }
        return RedirectToAction(nameof(Index));
    }
}

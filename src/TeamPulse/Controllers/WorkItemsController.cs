using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;

namespace TeamPulse.Controllers;

[Authorize]
public class WorkItemsController : Controller
{
    private readonly ApplicationDbContext _db;

    public WorkItemsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Board(int? teamId, int? sprintId)
    {
        var query = _db.WorkItems
            .Include(w => w.Team)
            .Include(w => w.AssignedMember)
            .Include(w => w.Sprint)
            .AsQueryable();
        if (teamId.HasValue) query = query.Where(w => w.TeamId == teamId);
        if (sprintId.HasValue) query = query.Where(w => w.SprintId == sprintId);

        ViewBag.Teams = await _db.Teams.OrderBy(t => t.Name).ToListAsync();
        ViewBag.Sprints = await _db.Sprints.OrderByDescending(s => s.Number).ToListAsync();
        ViewBag.SelectedTeam = teamId;
        ViewBag.SelectedSprint = sprintId;

        var items = await query.OrderByDescending(w => w.Priority).ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Index()
    {
        var items = await _db.WorkItems
            .Include(w => w.Team)
            .Include(w => w.AssignedMember)
            .Include(w => w.Sprint)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();
        return View(items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await _db.WorkItems
            .Include(w => w.Team)
            .Include(w => w.AssignedMember)
            .Include(w => w.Sprint)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (item == null) return NotFound();
        return View(item);
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create(int? teamId)
    {
        await Populate();
        return View(new WorkItem { TeamId = teamId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create(WorkItem item)
    {
        if (!ModelState.IsValid)
        {
            await Populate(item.TeamId, item.AssignedMemberId, item.SprintId);
            return View(item);
        }
        if (item.Status == WorkItemStatus.Done) item.CompletedAt = DateTime.UtcNow;
        _db.WorkItems.Add(item);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Work item created.";
        return RedirectToAction(nameof(Board));
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.WorkItems.FindAsync(id);
        if (item == null) return NotFound();
        await Populate(item.TeamId, item.AssignedMemberId, item.SprintId);
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id, WorkItem item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            await Populate(item.TeamId, item.AssignedMemberId, item.SprintId);
            return View(item);
        }
        var existing = await _db.WorkItems.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Title = item.Title;
        existing.Description = item.Description;
        existing.TeamId = item.TeamId;
        existing.AssignedMemberId = item.AssignedMemberId;
        existing.SprintId = item.SprintId;
        existing.Priority = item.Priority;
        existing.Type = item.Type;
        existing.StoryPoints = item.StoryPoints;
        existing.DueDate = item.DueDate;
        if (existing.Status != WorkItemStatus.Done && item.Status == WorkItemStatus.Done)
            existing.CompletedAt = DateTime.UtcNow;
        if (item.Status != WorkItemStatus.Done)
            existing.CompletedAt = null;
        existing.Status = item.Status;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Work item updated.";
        return RedirectToAction(nameof(Board));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> UpdateStatus(int id, WorkItemStatus status)
    {
        var item = await _db.WorkItems.FindAsync(id);
        if (item == null) return NotFound();
        item.Status = status;
        item.CompletedAt = status == WorkItemStatus.Done ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Board));
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.WorkItems.Include(w => w.Team).FirstOrDefaultAsync(w => w.Id == id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await _db.WorkItems.FindAsync(id);
        if (item != null)
        {
            _db.WorkItems.Remove(item);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Work item deleted.";
        }
        return RedirectToAction(nameof(Board));
    }

    private async Task Populate(int? team = null, int? member = null, int? sprint = null)
    {
        ViewBag.Teams = new SelectList(await _db.Teams.OrderBy(t => t.Name).ToListAsync(), "Id", "Name", team);
        ViewBag.Members = new SelectList(await _db.Members.OrderBy(m => m.FullName).ToListAsync(), "Id", "FullName", member);
        ViewBag.Sprints = new SelectList(await _db.Sprints.OrderByDescending(s => s.Number).ToListAsync(), "Id", "Name", sprint);
    }
}

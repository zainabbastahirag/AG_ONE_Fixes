using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;

namespace TeamPulse.Controllers;

[Authorize]
public class TeamsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public TeamsController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var teams = await _db.Teams
            .Include(t => t.TechLead)
            .Include(t => t.Members)
            .Include(t => t.WorkItems)
            .Include(t => t.Releases)
            .OrderBy(t => t.Name)
            .ToListAsync();
        return View(teams);
    }

    public async Task<IActionResult> Details(int id)
    {
        var team = await _db.Teams
            .Include(t => t.TechLead)
            .Include(t => t.Members).ThenInclude(m => m.AssignedWorkItems)
            .Include(t => t.WorkItems)
            .Include(t => t.Releases)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();
        return View(team);
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create()
    {
        await PopulateLeads();
        return View(new Team());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create(Team team)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLeads(team.TechLeadUserId);
            return View(team);
        }
        _db.Teams.Add(team);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Team \"{team.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id)
    {
        var team = await _db.Teams.FindAsync(id);
        if (team == null) return NotFound();
        await PopulateLeads(team.TechLeadUserId);
        return View(team);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id, Team team)
    {
        if (id != team.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateLeads(team.TechLeadUserId);
            return View(team);
        }
        var existing = await _db.Teams.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Name = team.Name;
        existing.Description = team.Description;
        existing.CurrentFocus = team.CurrentFocus;
        existing.KeyBlocker = team.KeyBlocker;
        existing.ColorHex = team.ColorHex;
        existing.Status = team.Status;
        existing.IsExternal = team.IsExternal;
        existing.TechLeadUserId = team.TechLeadUserId;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Team \"{team.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var team = await _db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();
        return View(team);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var team = await _db.Teams.FindAsync(id);
        if (team != null)
        {
            _db.Teams.Remove(team);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Team \"{team.Name}\" deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateLeads(string? selected = null)
    {
        var leads = await _users.GetUsersInRoleAsync("TechLead");
        var admins = await _users.GetUsersInRoleAsync("Admin");
        var all = leads.Concat(admins).DistinctBy(u => u.Id).OrderBy(u => u.FullName);
        ViewBag.Leads = new SelectList(all.Select(u => new { u.Id, Name = $"{u.FullName} ({u.Email})" }), "Id", "Name", selected);
    }
}

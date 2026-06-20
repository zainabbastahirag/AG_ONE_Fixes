using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;

namespace TeamPulse.Controllers;

[Authorize]
public class MembersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public MembersController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index(int? teamId)
    {
        var query = _db.Members
            .Include(m => m.Team)
            .Include(m => m.AssignedWorkItems)
            .AsQueryable();
        if (teamId.HasValue)
            query = query.Where(m => m.TeamId == teamId);

        ViewBag.Teams = await _db.Teams.OrderBy(t => t.Name).ToListAsync();
        ViewBag.SelectedTeam = teamId;
        var members = await query.OrderBy(m => m.FullName).ToListAsync();
        return View(members);
    }

    public async Task<IActionResult> Details(int id)
    {
        var member = await _db.Members
            .Include(m => m.Team)
            .Include(m => m.AssignedWorkItems).ThenInclude(w => w.Sprint)
            .Include(m => m.Reviews).ThenInclude(r => r.Reviewer)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (member == null) return NotFound();
        return View(member);
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create()
    {
        await Populate();
        return View(new Member());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Create(Member member)
    {
        if (!ModelState.IsValid)
        {
            await Populate(member.TeamId, member.ApplicationUserId);
            return View(member);
        }
        _db.Members.Add(member);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{member.FullName} added.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id)
    {
        var member = await _db.Members.FindAsync(id);
        if (member == null) return NotFound();
        await Populate(member.TeamId, member.ApplicationUserId);
        return View(member);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Edit(int id, Member member)
    {
        if (id != member.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            await Populate(member.TeamId, member.ApplicationUserId);
            return View(member);
        }
        var existing = await _db.Members.FindAsync(id);
        if (existing == null) return NotFound();
        existing.FullName = member.FullName;
        existing.Email = member.Email;
        existing.RoleTitle = member.RoleTitle;
        existing.TeamId = member.TeamId;
        existing.AllocationPercent = member.AllocationPercent;
        existing.ApplicationUserId = member.ApplicationUserId;
        existing.IsActive = member.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{member.FullName} updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> Delete(int id)
    {
        var member = await _db.Members.Include(m => m.Team).FirstOrDefaultAsync(m => m.Id == id);
        if (member == null) return NotFound();
        return View(member);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TechLead")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var member = await _db.Members.FindAsync(id);
        if (member != null)
        {
            _db.Members.Remove(member);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"{member.FullName} removed.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task Populate(int? team = null, string? user = null)
    {
        ViewBag.Teams = new SelectList(await _db.Teams.OrderBy(t => t.Name).ToListAsync(), "Id", "Name", team);
        var users = await _users.Users.OrderBy(u => u.FullName).ToListAsync();
        ViewBag.Users = new SelectList(users.Select(u => new { u.Id, Name = $"{u.FullName} ({u.Email})" }), "Id", "Name", user);
    }
}

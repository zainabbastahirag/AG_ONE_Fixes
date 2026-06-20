using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;

namespace TeamPulse.Controllers;

[Authorize(Roles = "Admin")]
public class InvitationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public InvitationsController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var invites = await _db.Invitations
            .Include(i => i.Team)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
        await Populate();
        return View(invites);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Invitation invitation)
    {
        if (string.IsNullOrWhiteSpace(invitation.Email))
        {
            TempData["Error"] = "Email is required.";
            return RedirectToAction(nameof(Index));
        }
        if (await _users.FindByEmailAsync(invitation.Email) != null)
        {
            TempData["Error"] = "A user with that email already exists.";
            return RedirectToAction(nameof(Index));
        }
        invitation.Token = Guid.NewGuid().ToString("N");
        invitation.Status = InvitationStatus.Pending;
        invitation.CreatedByUserId = _users.GetUserId(User);
        invitation.CreatedAt = DateTime.UtcNow;
        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Invitation created for {invitation.Email}. Share the invite link below.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id)
    {
        var invite = await _db.Invitations.FindAsync(id);
        if (invite != null && invite.Status == InvitationStatus.Pending)
        {
            invite.Status = InvitationStatus.Revoked;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Invitation revoked.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task Populate()
    {
        ViewBag.Teams = new SelectList(await _db.Teams.OrderBy(t => t.Name).ToListAsync(), "Id", "Name");
        ViewBag.Roles = new SelectList(DbSeeder.Roles);
    }
}

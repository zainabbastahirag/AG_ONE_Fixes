using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;
using TeamPulse.Models.ViewModels;

namespace TeamPulse.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public AccountController(ApplicationDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signIn.IsSignedIn(User))
            return RedirectToAction("Index", "Dashboard");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signIn.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Dashboard");
        }
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> AcceptInvite(string token)
    {
        var invite = await _db.Invitations.Include(i => i.Team).FirstOrDefaultAsync(i => i.Token == token);
        if (invite == null || invite.Status != InvitationStatus.Pending)
            return View("InviteInvalid");

        return View(new AcceptInviteViewModel
        {
            Token = invite.Token,
            Email = invite.Email,
            Role = invite.Role,
            TeamName = invite.Team?.Name
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvite(AcceptInviteViewModel model)
    {
        var invite = await _db.Invitations.FirstOrDefaultAsync(i => i.Token == model.Token);
        if (invite == null || invite.Status != InvitationStatus.Pending)
            return View("InviteInvalid");

        if (!ModelState.IsValid)
        {
            model.Email = invite.Email;
            model.Role = invite.Role;
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = invite.Email,
            Email = invite.Email,
            EmailConfirmed = true,
            FullName = model.FullName,
            JobTitle = model.JobTitle ?? invite.Role,
            AvatarColor = UiHelpers.ColorFor(model.FullName)
        };

        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            model.Email = invite.Email;
            model.Role = invite.Role;
            return View(model);
        }

        var role = DbSeeder.Roles.Contains(invite.Role) ? invite.Role : "Member";
        await _users.AddToRoleAsync(user, role);

        if (invite.TeamId.HasValue && role == "TechLead")
        {
            var team = await _db.Teams.FindAsync(invite.TeamId.Value);
            if (team != null) team.TechLeadUserId = user.Id;
        }

        invite.Status = InvitationStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _signIn.SignInAsync(user, isPersistent: false);
        TempData["Success"] = $"Welcome aboard, {user.FullName}!";
        return RedirectToAction("Index", "Dashboard");
    }
}

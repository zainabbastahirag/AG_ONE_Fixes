using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Assessments;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class RoadmapController : AgControllerBase
{
    private readonly IRoadmapService _roadmap;

    public RoadmapController(IAgOneSafeDbContext db, IRoadmapService roadmap) : base(db) =>
        _roadmap = roadmap;

    public async Task<IActionResult> Index(ProfileLevel? targetLevel, CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        var level = targetLevel ?? tenant.TargetLevel;
        ViewBag.SelectedLevel = level;

        return View(await _roadmap.BuildAsync(tenant.Id, level, cancellationToken));
    }

    /// <summary>
    /// Persists the customer's chosen maturity target. Everything downstream - scan scope, score,
    /// roadmap - keys off this single value.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanApprove)]
    public async Task<IActionResult> SetTarget(ProfileLevel targetLevel, CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        var tracked = await Db.TenantConnections.FirstAsync(t => t.Id == tenant.Id, cancellationToken);
        tracked.TargetLevel = targetLevel;
        await Db.SaveChangesAsync(cancellationToken);

        TempData["Toast"] = $"Target maturity set to CIS Level {(int)targetLevel}. Re-run the baseline scan to rescore.";
        return RedirectToAction(nameof(Index), new { targetLevel });
    }
}

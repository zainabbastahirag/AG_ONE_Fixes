using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;
using TeamPulse.Models.ViewModels;

namespace TeamPulse.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var teams = await _db.Teams
            .Include(t => t.Members)
            .Include(t => t.TechLead)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var workItems = await _db.WorkItems.ToListAsync();
        var releases = await _db.Releases.Include(r => r.Team).ToListAsync();
        var reviews = await _db.PerformanceReviews.ToListAsync();

        var vm = new DashboardViewModel
        {
            TotalTeams = teams.Count,
            TotalMembers = await _db.Members.CountAsync(m => m.IsActive),
            TeamsOnTrack = teams.Count(t => t.Status == TeamStatus.OnTrack),
            TeamsAtRisk = teams.Count(t => t.Status is TeamStatus.AtRisk or TeamStatus.Blocked),
            TeamsOnHold = teams.Count(t => t.Status == TeamStatus.OnHold),
            OpenWorkItems = workItems.Count(w => w.Status != WorkItemStatus.Done),
            BlockedWorkItems = workItems.Count(w => w.Status == WorkItemStatus.Blocked),
            CompletedWorkItems = workItems.Count(w => w.Status == WorkItemStatus.Done),
            UpcomingReleases = releases.Count(r => r.Status is ReleaseStatus.Planned or ReleaseStatus.InProgress or ReleaseStatus.Testing),
            ReviewsThisQuarter = reviews.Count,
            Teams = teams,
            RecentReleases = releases
                .OrderByDescending(r => r.TargetDate ?? r.ReleasedDate ?? r.CreatedAt)
                .Take(5).ToList(),
            AverageRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.OverallRating), 2) : 0
        };

        return View(vm);
    }
}

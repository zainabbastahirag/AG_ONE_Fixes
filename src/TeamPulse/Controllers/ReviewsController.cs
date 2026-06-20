using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TeamPulse.Data;
using TeamPulse.Models.Domain;
using TeamPulse.Models.ViewModels;

namespace TeamPulse.Controllers;

[Authorize]
public class ReviewsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ReviewsController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IActionResult> Index(int? memberId, int? sprintId)
    {
        var query = _db.PerformanceReviews
            .Include(r => r.Member).ThenInclude(m => m!.Team)
            .Include(r => r.Reviewer)
            .Include(r => r.Sprint)
            .AsQueryable();
        if (memberId.HasValue) query = query.Where(r => r.MemberId == memberId);
        if (sprintId.HasValue) query = query.Where(r => r.SprintId == sprintId);

        ViewBag.Members = await _db.Members.OrderBy(m => m.FullName).ToListAsync();
        ViewBag.Sprints = await _db.Sprints.OrderByDescending(s => s.Number).ToListAsync();
        ViewBag.SelectedMember = memberId;
        ViewBag.SelectedSprint = sprintId;

        var reviews = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(reviews);
    }

    public async Task<IActionResult> MyQueue()
    {
        var userId = _users.GetUserId(User)!;
        var assignedMemberIds = await _db.ReviewerAssignments
            .Where(a => a.ReviewerUserId == userId)
            .Select(a => a.MemberId)
            .ToListAsync();

        var assignments = await _db.ReviewerAssignments
            .Include(a => a.Member).ThenInclude(m => m!.Team)
            .Where(a => a.ReviewerUserId == userId)
            .ToListAsync();

        var authored = await _db.PerformanceReviews
            .Include(r => r.Member)
            .Include(r => r.Sprint)
            .Where(r => r.ReviewerUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        ViewBag.Authored = authored;
        return View(assignments);
    }

    public async Task<IActionResult> Details(int id)
    {
        var review = await _db.PerformanceReviews
            .Include(r => r.Member).ThenInclude(m => m!.Team)
            .Include(r => r.Reviewer)
            .Include(r => r.Sprint)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (review == null) return NotFound();
        return View(review);
    }

    public async Task<IActionResult> Create(int? memberId)
    {
        if (memberId.HasValue && !await CanReview(memberId.Value))
            return Forbid();

        await Populate();
        var activeSprint = await _db.Sprints.FirstOrDefaultAsync(s => s.IsActive);
        return View(new PerformanceReview
        {
            MemberId = memberId ?? 0,
            SprintId = activeSprint?.Id,
            Quarter = activeSprint?.Quarter ?? ((DateTime.UtcNow.Month - 1) / 3 + 1),
            Year = DateTime.UtcNow.Year
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PerformanceReview review)
    {
        if (!await CanReview(review.MemberId))
        {
            TempData["Error"] = "You are not assigned to review this member.";
            return RedirectToAction(nameof(Index));
        }
        if (!ModelState.IsValid)
        {
            await Populate();
            return View(review);
        }
        review.ReviewerUserId = _users.GetUserId(User);
        review.CreatedAt = DateTime.UtcNow;
        _db.PerformanceReviews.Add(review);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Performance review submitted.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var review = await _db.PerformanceReviews.FindAsync(id);
        if (review == null) return NotFound();
        if (!await CanEdit(review)) return Forbid();
        await Populate();
        return View(review);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PerformanceReview review)
    {
        if (id != review.Id) return NotFound();
        var existing = await _db.PerformanceReviews.FindAsync(id);
        if (existing == null) return NotFound();
        if (!await CanEdit(existing)) return Forbid();
        if (!ModelState.IsValid)
        {
            await Populate();
            return View(review);
        }
        existing.MemberId = review.MemberId;
        existing.SprintId = review.SprintId;
        existing.PeriodType = review.PeriodType;
        existing.Quarter = review.Quarter;
        existing.Year = review.Year;
        existing.RatingDelivery = review.RatingDelivery;
        existing.RatingQuality = review.RatingQuality;
        existing.RatingCollaboration = review.RatingCollaboration;
        existing.RatingOwnership = review.RatingOwnership;
        existing.RatingInnovation = review.RatingInnovation;
        existing.Strengths = review.Strengths;
        existing.Improvements = review.Improvements;
        existing.Comments = review.Comments;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Review updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var review = await _db.PerformanceReviews
            .Include(r => r.Member).Include(r => r.Reviewer)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (review == null) return NotFound();
        if (!await CanEdit(review)) return Forbid();
        return View(review);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var review = await _db.PerformanceReviews.FindAsync(id);
        if (review == null) return NotFound();
        if (!await CanEdit(review)) return Forbid();
        _db.PerformanceReviews.Remove(review);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Review deleted.";
        return RedirectToAction(nameof(Index));
    }

    // -------- Reviewer assignments (Admin) --------

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Assignments()
    {
        var assignments = await _db.ReviewerAssignments
            .Include(a => a.Reviewer)
            .Include(a => a.Member).ThenInclude(m => m!.Team)
            .OrderBy(a => a.Reviewer!.FullName)
            .ToListAsync();
        await PopulateAssignment();
        return View(assignments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignReviewer(string reviewerUserId, int memberId, string? note)
    {
        if (string.IsNullOrEmpty(reviewerUserId) || memberId == 0)
        {
            TempData["Error"] = "Select both a reviewer and a member.";
            return RedirectToAction(nameof(Assignments));
        }
        var exists = await _db.ReviewerAssignments
            .AnyAsync(a => a.ReviewerUserId == reviewerUserId && a.MemberId == memberId);
        if (exists)
        {
            TempData["Error"] = "That reviewer is already assigned to this member.";
            return RedirectToAction(nameof(Assignments));
        }
        _db.ReviewerAssignments.Add(new ReviewerAssignment
        {
            ReviewerUserId = reviewerUserId,
            MemberId = memberId,
            Note = note
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Reviewer assigned.";
        return RedirectToAction(nameof(Assignments));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveAssignment(int id)
    {
        var assignment = await _db.ReviewerAssignments.FindAsync(id);
        if (assignment != null)
        {
            _db.ReviewerAssignments.Remove(assignment);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Assignment removed.";
        }
        return RedirectToAction(nameof(Assignments));
    }

    // -------- Helpers --------

    private async Task<bool> CanReview(int memberId)
    {
        if (User.IsInRole("Admin")) return true;
        var userId = _users.GetUserId(User)!;

        if (await _db.ReviewerAssignments.AnyAsync(a => a.ReviewerUserId == userId && a.MemberId == memberId))
            return true;

        if (User.IsInRole("TechLead"))
        {
            var member = await _db.Members.FindAsync(memberId);
            if (member?.TeamId != null)
            {
                var team = await _db.Teams.FindAsync(member.TeamId);
                if (team?.TechLeadUserId == userId) return true;
            }
        }
        return false;
    }

    private async Task<bool> CanEdit(PerformanceReview review)
    {
        if (User.IsInRole("Admin")) return true;
        var userId = _users.GetUserId(User);
        return await Task.FromResult(review.ReviewerUserId == userId);
    }

    private async Task Populate()
    {
        var userId = _users.GetUserId(User)!;
        IEnumerable<Member> members;
        if (User.IsInRole("Admin"))
        {
            members = await _db.Members.Include(m => m.Team).OrderBy(m => m.FullName).ToListAsync();
        }
        else
        {
            var assigned = await _db.ReviewerAssignments
                .Where(a => a.ReviewerUserId == userId).Select(a => a.MemberId).ToListAsync();
            var ledTeamIds = await _db.Teams.Where(t => t.TechLeadUserId == userId).Select(t => t.Id).ToListAsync();
            members = await _db.Members.Include(m => m.Team)
                .Where(m => assigned.Contains(m.Id) || (m.TeamId != null && ledTeamIds.Contains(m.TeamId.Value)))
                .OrderBy(m => m.FullName).ToListAsync();
        }
        ViewBag.Members = new SelectList(members.Select(m => new { m.Id, Name = $"{m.FullName} — {m.Team?.Name ?? "Unassigned"}" }), "Id", "Name");
        ViewBag.Sprints = new SelectList(await _db.Sprints.OrderByDescending(s => s.Number).ToListAsync(), "Id", "Name");
    }

    private async Task PopulateAssignment()
    {
        var reviewers = await _users.Users.OrderBy(u => u.FullName).ToListAsync();
        ViewBag.Reviewers = new SelectList(reviewers.Select(u => new { u.Id, Name = $"{u.FullName} ({u.Email})" }), "Id", "Name");
        ViewBag.MemberOptions = new SelectList(
            (await _db.Members.Include(m => m.Team).OrderBy(m => m.FullName).ToListAsync())
                .Select(m => new { m.Id, Name = $"{m.FullName} — {m.Team?.Name ?? "Unassigned"}" }), "Id", "Name");
    }
}

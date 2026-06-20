using Microsoft.AspNetCore.Identity;

namespace TeamPulse.Models.Domain;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string AvatarColor { get; set; } = "#4f46e5";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Team> LedTeams { get; set; } = new List<Team>();
    public ICollection<PerformanceReview> AuthoredReviews { get; set; } = new List<PerformanceReview>();
    public ICollection<ReviewerAssignment> ReviewerAssignments { get; set; } = new List<ReviewerAssignment>();
}

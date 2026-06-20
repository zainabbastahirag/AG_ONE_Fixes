using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.Domain;

public class Member
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(160)]
    public string? Email { get; set; }

    [Display(Name = "Role / Title")]
    [StringLength(120)]
    public string? RoleTitle { get; set; }

    [Display(Name = "Team")]
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    [Range(0, 100)]
    [Display(Name = "Allocation %")]
    public int AllocationPercent { get; set; } = 100;

    [Display(Name = "Linked Login Account")]
    public string? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkItem> AssignedWorkItems { get; set; } = new List<WorkItem>();
    public ICollection<PerformanceReview> Reviews { get; set; } = new List<PerformanceReview>();
}

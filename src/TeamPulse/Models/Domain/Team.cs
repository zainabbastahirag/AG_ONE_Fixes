using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.Domain;

public class Team
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(280)]
    public string? Description { get; set; }

    [Display(Name = "Current Focus")]
    [StringLength(600)]
    public string? CurrentFocus { get; set; }

    [Display(Name = "Key Blocker")]
    [StringLength(600)]
    public string? KeyBlocker { get; set; }

    [StringLength(9)]
    public string ColorHex { get; set; } = "#4f46e5";

    public TeamStatus Status { get; set; } = TeamStatus.OnTrack;

    [Display(Name = "External Project")]
    public bool IsExternal { get; set; }

    [Display(Name = "Tech Lead")]
    public string? TechLeadUserId { get; set; }
    public ApplicationUser? TechLead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    public ICollection<Release> Releases { get; set; } = new List<Release>();
}

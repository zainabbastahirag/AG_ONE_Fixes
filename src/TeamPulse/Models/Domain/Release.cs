using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.Domain;

public class Release
{
    public int Id { get; set; }

    [Required]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Version { get; set; }

    [Display(Name = "Team")]
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public ReleaseStatus Status { get; set; } = ReleaseStatus.Planned;

    [Display(Name = "Target Date")]
    [DataType(DataType.Date)]
    public DateTime? TargetDate { get; set; }

    [Display(Name = "Released Date")]
    [DataType(DataType.Date)]
    public DateTime? ReleasedDate { get; set; }

    [Range(0, 100)]
    [Display(Name = "Progress %")]
    public int ProgressPercent { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

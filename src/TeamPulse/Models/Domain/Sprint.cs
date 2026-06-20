using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.Domain;

public class Sprint
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Sprint Number")]
    public int Number { get; set; }

    [Range(1, 4)]
    public int Quarter { get; set; } = 1;

    [Range(2020, 2100)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [Display(Name = "Start Date")]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

    [Display(Name = "End Date")]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date.AddDays(14);

    [StringLength(400)]
    public string? Goal { get; set; }

    [Display(Name = "Active Sprint")]
    public bool IsActive { get; set; }

    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    public ICollection<PerformanceReview> Reviews { get; set; } = new List<PerformanceReview>();
}

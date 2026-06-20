using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.Domain;

public class PerformanceReview
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Member")]
    public int MemberId { get; set; }
    public Member? Member { get; set; }

    [Display(Name = "Reviewer")]
    public string? ReviewerUserId { get; set; }
    public ApplicationUser? Reviewer { get; set; }

    [Display(Name = "Sprint")]
    public int? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    [Display(Name = "Review Period")]
    public ReviewPeriodType PeriodType { get; set; } = ReviewPeriodType.Sprint;

    [Range(1, 4)]
    public int Quarter { get; set; } = 1;

    [Range(2020, 2100)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [Range(1, 5)]
    [Display(Name = "Delivery")]
    public int RatingDelivery { get; set; } = 3;

    [Range(1, 5)]
    [Display(Name = "Quality")]
    public int RatingQuality { get; set; } = 3;

    [Range(1, 5)]
    [Display(Name = "Collaboration")]
    public int RatingCollaboration { get; set; } = 3;

    [Range(1, 5)]
    [Display(Name = "Ownership")]
    public int RatingOwnership { get; set; } = 3;

    [Range(1, 5)]
    [Display(Name = "Innovation")]
    public int RatingInnovation { get; set; } = 3;

    [StringLength(2000)]
    public string? Strengths { get; set; }

    [StringLength(2000)]
    public string? Improvements { get; set; }

    [StringLength(2000)]
    public string? Comments { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public double OverallRating =>
        Math.Round((RatingDelivery + RatingQuality + RatingCollaboration + RatingOwnership + RatingInnovation) / 5.0, 2);
}

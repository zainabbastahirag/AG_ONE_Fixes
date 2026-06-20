using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.Domain;

public class ReviewerAssignment
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Reviewer")]
    public string ReviewerUserId { get; set; } = string.Empty;
    public ApplicationUser? Reviewer { get; set; }

    [Required]
    [Display(Name = "Member to Review")]
    public int MemberId { get; set; }
    public Member? Member { get; set; }

    [StringLength(280)]
    public string? Note { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

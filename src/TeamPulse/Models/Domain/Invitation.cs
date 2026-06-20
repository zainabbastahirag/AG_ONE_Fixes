using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.Domain;

public class Invitation
{
    public int Id { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string Role { get; set; } = "TechLead";

    [Required]
    [StringLength(64)]
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    [Display(Name = "Assign to Team")]
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    [StringLength(280)]
    public string? Message { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
}

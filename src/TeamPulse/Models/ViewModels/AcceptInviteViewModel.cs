using System.ComponentModel.DataAnnotations;

namespace TeamPulse.Models.ViewModels;

public class AcceptInviteViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? TeamName { get; set; }

    [Required]
    [Display(Name = "Full Name")]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Job Title")]
    [StringLength(120)]
    public string? JobTitle { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

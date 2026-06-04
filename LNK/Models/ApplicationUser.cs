using Microsoft.AspNetCore.Identity;

namespace LNK.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public bool OnboardingCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserSettings? Settings { get; set; }
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public Schedule? Schedule { get; set; }
}

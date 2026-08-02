using Microsoft.AspNetCore.Identity;

namespace AgOneSafe.Domain.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public string JobTitle { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Role names used by the HITL approval gates.</summary>
public static class AgRoles
{
    /// <summary>Approves High and Critical change, signs off risk acceptance.</summary>
    public const string Ciso = "CISO";

    /// <summary>Plans, dry-runs and executes remediation; approves Medium change.</summary>
    public const string SecurityAdmin = "SecurityAdmin";

    /// <summary>Read-only access to posture, findings and the proof centre.</summary>
    public const string Auditor = "Auditor";

    public static readonly string[] All = { Ciso, SecurityAdmin, Auditor };

    public const string CanApprove = Ciso + "," + SecurityAdmin;
    public const string CanExecute = Ciso + "," + SecurityAdmin;
}

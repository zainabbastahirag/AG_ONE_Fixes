namespace AGOne.Authorization.Models;

public sealed record UserPermissionSet
{
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public long Version { get; init; }
    public HashSet<string> Permissions { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime LoadedAt { get; init; } = DateTime.UtcNow;
}

public sealed record PermissionCheckResult(bool Granted, string? Reason = null);

public sealed class AGOneAuthOptions
{
    /// <summary>
    /// AG ONE gateway base URL for token refresh and permission sync.
    /// Used by downstream apps (Work, Learn, Safe, etc.) to call AG ONE.
    /// </summary>
    public string GatewayBaseUrl { get; set; } = "";

    /// <summary>
    /// How long to cache permissions in memory before re-checking.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// If true, this is the AG ONE gateway itself (reads from DB).
    /// If false, this is a downstream product (reads from JWT + HTTP refresh).
    /// </summary>
    public bool IsGateway { get; set; }

    /// <summary>
    /// JWT signing key (shared across all products for validation).
    /// </summary>
    public string JwtSecret { get; set; } = "";

    /// <summary>
    /// JWT issuer.
    /// </summary>
    public string JwtIssuer { get; set; } = "agone";

    /// <summary>
    /// JWT audience.
    /// </summary>
    public string JwtAudience { get; set; } = "agone-products";
}

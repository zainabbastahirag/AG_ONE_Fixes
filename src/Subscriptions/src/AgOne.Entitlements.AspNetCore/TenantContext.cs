namespace AgOne.Entitlements.AspNetCore;

/// <summary>
/// Ambient tenant + user identity for the current request. Populated by
/// <see cref="TenantResolutionMiddleware"/> from the authenticated principal's claims.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
    bool IsResolved { get; }
}

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; internal set; }
    public Guid UserId { get; internal set; }
    public bool IsResolved { get; internal set; }
}

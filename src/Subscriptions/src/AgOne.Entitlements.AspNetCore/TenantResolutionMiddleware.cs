using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AgOne.Entitlements.AspNetCore;

/// <summary>
/// Reads the tenant and user id from the authenticated principal and populates the
/// scoped <see cref="TenantContext"/>. Adjust the claim types to match your token/cookie.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string TenantClaimType = "tenant_id";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, TenantContext tenant)
    {
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            if (TryGetGuid(user, TenantClaimType, out var tenantId))
                tenant.TenantId = tenantId;

            if (TryGetGuid(user, ClaimTypes.NameIdentifier, out var userId) ||
                TryGetGuid(user, "sub", out userId))
                tenant.UserId = userId;

            tenant.IsResolved = tenant.TenantId != Guid.Empty && tenant.UserId != Guid.Empty;
        }

        await _next(context);
    }

    private static bool TryGetGuid(ClaimsPrincipal user, string claimType, out Guid value)
    {
        var raw = user.FindFirstValue(claimType);
        return Guid.TryParse(raw, out value);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// FILE: AGOneServerPermissionService.cs
// GOES IN: API project (or Infrastructure project)
//
// This file references IHttpContextAccessor, IMemoryCache, and
// ASP.NET Core Authorization — all available via the server runtime.
//
// API/Infrastructure .csproj needs:
//   <FrameworkReference Include="Microsoft.AspNetCore.App" />
//   (or it's already an ASP.NET Core Web project which has it automatically)
//
// Also references IAGOnePermissionService from Shared project.
// ═══════════════════════════════════════════════════════════════════════════

using System.Security.Claims;
using AGOne.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace AGOne.Api.Authorization;

// ═══════════════════════════════════════════════════════════════════════════
// 1. SERVER IMPLEMENTATION — reads JWT claims from HttpContext
// ═══════════════════════════════════════════════════════════════════════════

public sealed class ServerPermissionService : IAGOnePermissionService
{
    private readonly IHttpContextAccessor _http;
    private readonly IMemoryCache _cache;

    public ServerPermissionService(IHttpContextAccessor http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    private ClaimsPrincipal User
        => _http.HttpContext?.User ?? throw new UnauthorizedAccessException("No HTTP context");

    public Guid GetUserId()
    {
        var v = User.FindFirst("sub")?.Value;
        return Guid.TryParse(v, out var id) ? id : throw new UnauthorizedAccessException("No sub claim");
    }

    public Guid GetTenantId()
    {
        var v = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(v, out var id) ? id : throw new UnauthorizedAccessException("No tenant_id claim");
    }

    public Guid? GetProductId()
    {
        var v = User.FindFirst("product_id")?.Value;
        return Guid.TryParse(v, out var id) ? id : null;
    }

    public string? GetPrimaryRole() => User.FindFirst("primary_role")?.Value;
    public List<string> GetRoles() => User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
    public List<string> GetProductRoles() => User.FindAll("product_role").Select(c => c.Value).ToList();

    public HashSet<string> GetPermissions()
    {
        var key = $"perms:{GetTenantId()}:{GetUserId()}";
        if (_cache.TryGetValue(key, out HashSet<string>? cached) && cached != null)
            return cached;

        var perms = User.FindAll("permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _cache.Set(key, perms, TimeSpan.FromMinutes(10));
        return perms;
    }

    public bool HasPermission(string code) => GetPermissions().Contains(code);
    public bool HasAll(params string[] codes) { var p = GetPermissions(); return codes.All(p.Contains); }
    public bool HasAny(params string[] codes) { var p = GetPermissions(); return codes.Any(p.Contains); }
}

// ═══════════════════════════════════════════════════════════════════════════
// 2. [RequirePermission] ATTRIBUTE — for API controllers
// ═══════════════════════════════════════════════════════════════════════════

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    internal const string Prefix = "AGPerm:";
    public RequirePermissionAttribute(string permission) { Policy = Prefix + permission; }
    public RequirePermissionAttribute(params string[] permissions) { Policy = Prefix + string.Join(",", permissions); }
}

internal sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;
    public PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> o)
        => _fallback = new DefaultAuthorizationPolicyProvider(o);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(RequirePermissionAttribute.Prefix))
            return _fallback.GetPolicyAsync(policyName);

        var codes = policyName[RequirePermissionAttribute.Prefix.Length..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermReq(codes))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

internal sealed record PermReq(string[] Codes) : IAuthorizationRequirement;

internal sealed class PermHandler : AuthorizationHandler<PermReq>
{
    private readonly IAGOnePermissionService _svc;
    public PermHandler(IAGOnePermissionService svc) => _svc = svc;
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext ctx, PermReq req)
    {
        if (ctx.User.Identity?.IsAuthenticated == true && _svc.HasAny(req.Codes))
            ctx.Succeed(req);
        return Task.CompletedTask;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 3. DI — call from API Program.cs
// ═══════════════════════════════════════════════════════════════════════════

public static class AGOneServerPermissionExtensions
{
    public static IServiceCollection AddAGOneServerPermissions(this IServiceCollection services)
    {
        services.AddScoped<IAGOnePermissionService, ServerPermissionService>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermHandler>();
        return services;
    }
}

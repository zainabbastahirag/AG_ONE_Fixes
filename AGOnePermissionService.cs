using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace AGOne.Shared.Authorization;

// ═══════════════════════════════════════════════════════════════════════════
// 1. PERMISSION CONSTANTS — use these everywhere
// ═══════════════════════════════════════════════════════════════════════════

public static class Permissions
{
    public const string ClaimType = "permissions";
    public const string VersionClaim = "perm_version";

    public static class AgOne
    {
        public static class Users   { public const string Create = "agone.users.create"; public const string Read = "agone.users.read"; public const string Update = "agone.users.update"; public const string Delete = "agone.users.delete"; }
        public static class Roles   { public const string Create = "agone.roles.create"; public const string Read = "agone.roles.read"; public const string Update = "agone.roles.update"; public const string Delete = "agone.roles.delete"; }
        public static class Perms   { public const string Create = "agone.permissions.create"; public const string Read = "agone.permissions.read"; public const string Update = "agone.permissions.update"; public const string Delete = "agone.permissions.delete"; }
        public static class Tenant  { public const string Create = "agone.tenant.create"; public const string Read = "agone.tenant.read"; public const string Update = "agone.tenant.update"; public const string Delete = "agone.tenant.delete"; }
        public static class Sub     { public const string Create = "agone.subscription.create"; public const string Read = "agone.subscription.read"; public const string Update = "agone.subscription.update"; public const string Delete = "agone.subscription.delete"; }
        public static class Billing { public const string Create = "agone.billing.create"; public const string Read = "agone.billing.read"; public const string Update = "agone.billing.update"; public const string Delete = "agone.billing.delete"; }
        public static class Master  { public const string Create = "agone.masterdata.create"; public const string Read = "agone.masterdata.read"; public const string Update = "agone.masterdata.update"; public const string Delete = "agone.masterdata.delete"; }
        public static class Audit   { public const string Read = "agone.audit.read"; }
    }

    public static class Work
    {
        public static class Employee  { public const string Create = "work.employee.create"; public const string Read = "work.employee.read"; public const string Update = "work.employee.update"; public const string Delete = "work.employee.delete"; }
        public static class Recruit   { public const string Create = "work.recruitment.create"; public const string Read = "work.recruitment.read"; public const string Update = "work.recruitment.update"; public const string Delete = "work.recruitment.delete"; }
        public static class Activate  { public const string Create = "work.activate.create"; public const string Read = "work.activate.read"; public const string Update = "work.activate.update"; public const string Delete = "work.activate.delete"; }
        public static class Master    { public const string Create = "work.masterdata.create"; public const string Read = "work.masterdata.read"; public const string Update = "work.masterdata.update"; public const string Delete = "work.masterdata.delete"; }
    }

    public static class Learn
    {
        public static class Path       { public const string Create = "learn.path.create"; public const string Read = "learn.path.read"; public const string Update = "learn.path.update"; public const string Delete = "learn.path.delete"; }
        public static class DataSource { public const string Create = "learn.datasource.create"; public const string Read = "learn.datasource.read"; public const string Update = "learn.datasource.update"; public const string Delete = "learn.datasource.delete"; }
        public static class Assignment { public const string Create = "learn.assignment.create"; public const string Read = "learn.assignment.read"; public const string Update = "learn.assignment.update"; public const string Delete = "learn.assignment.delete"; }
        public static class Assessment { public const string Create = "learn.assessment.create"; public const string Read = "learn.assessment.read"; public const string Update = "learn.assessment.update"; public const string Delete = "learn.assessment.delete"; }
    }

    public static class Safe
    {
        public static class Policy     { public const string Create = "safe.policy.create"; public const string Read = "safe.policy.read"; public const string Update = "safe.policy.update"; public const string Delete = "safe.policy.delete"; }
        public static class Compliance { public const string Create = "safe.compliance.create"; public const string Read = "safe.compliance.read"; public const string Update = "safe.compliance.update"; public const string Delete = "safe.compliance.delete"; }
        public static class DataLib    { public const string Create = "safe.datalibrary.create"; public const string Read = "safe.datalibrary.read"; public const string Update = "safe.datalibrary.update"; public const string Delete = "safe.datalibrary.delete"; }
    }

    public static class Pulse
    {
        public static class Survey    { public const string Create = "pulse.survey.create"; public const string Read = "pulse.survey.read"; public const string Update = "pulse.survey.update"; public const string Delete = "pulse.survey.delete"; }
        public static class Analytics { public const string Read = "pulse.analytics.read"; }
    }

    public static class Spot
    {
        public static class City { public const string Create = "spot.city.create"; public const string Read = "spot.city.read"; public const string Update = "spot.city.update"; public const string Delete = "spot.city.delete"; }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 2. INTERFACE — inject this anywhere: controllers, services, Blazor
// ═══════════════════════════════════════════════════════════════════════════

public interface IAGOnePermissionService
{
    Task<bool> HasPermissionAsync(string permissionCode);
    Task<bool> HasAllAsync(params string[] codes);
    Task<bool> HasAnyAsync(params string[] codes);
    HashSet<string> GetCurrentPermissions();
    long GetCurrentVersion();
    Task RefreshPermissionsAsync();
}

// ═══════════════════════════════════════════════════════════════════════════
// 3. IMPLEMENTATION — reads from JWT claims, caches, refreshes from gateway
// ═══════════════════════════════════════════════════════════════════════════

public sealed class AGOnePermissionService : IAGOnePermissionService
{
    private readonly IHttpContextAccessor _http;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILogger<AGOnePermissionService> _logger;
    private readonly AGOnePermissionOptions _options;

    public AGOnePermissionService(
        IHttpContextAccessor http,
        IMemoryCache cache,
        ILogger<AGOnePermissionService> logger,
        AGOnePermissionOptions options,
        IHttpClientFactory? httpClientFactory = null)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    private Guid GetUserId()
    {
        var sub = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User?.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("No user in context");
    }

    private Guid GetTenantId()
    {
        var tid = User?.FindFirst("tenant_id")?.Value ?? User?.FindFirst("tid")?.Value;
        return Guid.TryParse(tid, out var id) ? id : throw new UnauthorizedAccessException("No tenant in context");
    }

    public long GetCurrentVersion()
    {
        var v = User?.FindFirst(Permissions.VersionClaim)?.Value;
        return long.TryParse(v, out var ver) ? ver : 0;
    }

    public HashSet<string> GetCurrentPermissions()
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var cacheKey = $"perms:{tenantId}:{userId}";

        if (_cache.TryGetValue(cacheKey, out CachedPermissions? cached) && cached != null)
        {
            var jwtVersion = GetCurrentVersion();
            if (jwtVersion <= cached.Version)
                return cached.Codes;
        }

        var perms = User?.FindAll(Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var version = GetCurrentVersion();

        _cache.Set(cacheKey, new CachedPermissions(perms, version), _options.CacheDuration);

        return perms;
    }

    public Task<bool> HasPermissionAsync(string permissionCode)
    {
        return Task.FromResult(GetCurrentPermissions().Contains(permissionCode));
    }

    public Task<bool> HasAllAsync(params string[] codes)
    {
        var perms = GetCurrentPermissions();
        return Task.FromResult(codes.All(c => perms.Contains(c)));
    }

    public Task<bool> HasAnyAsync(params string[] codes)
    {
        var perms = GetCurrentPermissions();
        return Task.FromResult(codes.Any(c => perms.Contains(c)));
    }

    public async Task RefreshPermissionsAsync()
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var cacheKey = $"perms:{tenantId}:{userId}";

        _cache.Remove(cacheKey);

        if (_httpClientFactory != null && !string.IsNullOrEmpty(_options.GatewayBaseUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("AGOneGateway");
                var result = await client.GetFromJsonAsync<GatewayPermissionResponse>(
                    $"api/internal/permissions/{tenantId}/{userId}");

                if (result != null)
                {
                    var perms = new HashSet<string>(result.Permissions, StringComparer.OrdinalIgnoreCase);
                    _cache.Set(cacheKey, new CachedPermissions(perms, result.Version), _options.CacheDuration);
                    _logger.LogInformation("Refreshed permissions from gateway for user {UserId}, version {V}", userId, result.Version);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh permissions from gateway, falling back to JWT claims");
            }
        }

        GetCurrentPermissions();
    }

    private sealed record CachedPermissions(HashSet<string> Codes, long Version);
    private sealed record GatewayPermissionResponse(HashSet<string> Permissions, long Version);
}

// ═══════════════════════════════════════════════════════════════════════════
// 4. OPTIONS
// ═══════════════════════════════════════════════════════════════════════════

public sealed class AGOnePermissionOptions
{
    /// <summary>AG ONE gateway URL — only needed by downstream apps (Work/Learn/Safe).</summary>
    public string? GatewayBaseUrl { get; set; }

    /// <summary>How long to cache permissions in memory. Default: 5 minutes.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}

// ═══════════════════════════════════════════════════════════════════════════
// 5. [RequirePermission] ATTRIBUTE — use on controllers / actions
// ═══════════════════════════════════════════════════════════════════════════

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    internal const string Prefix = "AGPerm:";

    public RequirePermissionAttribute(string permission)
    {
        Policy = Prefix + permission;
    }

    public RequirePermissionAttribute(params string[] permissions)
    {
        Policy = Prefix + string.Join(",", permissions);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 6. POLICY PROVIDER + HANDLER — wires up the attribute automatically
// ═══════════════════════════════════════════════════════════════════════════

internal sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> opts)
        => _fallback = new DefaultAuthorizationPolicyProvider(opts);

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
            .AddRequirements(new PermissionRequirement(codes))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

internal sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string[] Codes { get; }
    public PermissionRequirement(string[] codes) => Codes = codes;
}

internal sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IAGOnePermissionService _svc;
    public PermissionHandler(IAGOnePermissionService svc) => _svc = svc;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext ctx, PermissionRequirement req)
    {
        if (ctx.User.Identity?.IsAuthenticated != true) return;
        if (await _svc.HasAnyAsync(req.Codes))
            ctx.Succeed(req);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 7. DI REGISTRATION — one line in Program.cs
// ═══════════════════════════════════════════════════════════════════════════

public static class AGOnePermissionExtensions
{
    /// <summary>
    /// Register the shared permission service. Call in every product's Program.cs.
    ///
    /// AG ONE gateway:
    ///   builder.Services.AddAGOnePermissions();
    ///
    /// Downstream (Work/Learn/Safe/Pulse/Spot):
    ///   builder.Services.AddAGOnePermissions(o => o.GatewayBaseUrl = "https://agone.example.com");
    /// </summary>
    public static IServiceCollection AddAGOnePermissions(
        this IServiceCollection services,
        Action<AGOnePermissionOptions>? configure = null)
    {
        var options = new AGOnePermissionOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        if (!string.IsNullOrEmpty(options.GatewayBaseUrl))
        {
            services.AddHttpClient("AGOneGateway", c =>
            {
                c.BaseAddress = new Uri(options.GatewayBaseUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(10);
            });
        }

        services.AddScoped<IAGOnePermissionService, AGOnePermissionService>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        return services;
    }
}

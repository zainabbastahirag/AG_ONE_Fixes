// ═══════════════════════════════════════════════════════════════════════════
// FILE: AGOnePermissionService.cs
// GOES IN: Shared project
//
// Shared .csproj only needs these NuGet packages (all WASM-compatible):
//   <PackageReference Include="Microsoft.AspNetCore.Components.Authorization" Version="8.0.*" />
//   <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.*" />
//
// NO <FrameworkReference Include="Microsoft.AspNetCore.App" /> needed!
// ═══════════════════════════════════════════════════════════════════════════

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AGOne.Shared.Authorization;

// ═══════════════════════════════════════════════════════════════════════════
// 1. PERMISSION CONSTANTS
// ═══════════════════════════════════════════════════════════════════════════

public static class Permissions
{
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
        public static class Employee { public const string Create = "work.employee.create"; public const string Read = "work.employee.read"; public const string Update = "work.employee.update"; public const string Delete = "work.employee.delete"; }
        public static class Recruit  { public const string Create = "work.recruitment.create"; public const string Read = "work.recruitment.read"; public const string Update = "work.recruitment.update"; public const string Delete = "work.recruitment.delete"; }
        public static class Activate { public const string Create = "work.activate.create"; public const string Read = "work.activate.read"; public const string Update = "work.activate.update"; public const string Delete = "work.activate.delete"; }
        public static class Master   { public const string Create = "work.masterdata.create"; public const string Read = "work.masterdata.read"; public const string Update = "work.masterdata.update"; public const string Delete = "work.masterdata.delete"; }
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
// 2. INTERFACE — used by both API and UI
// ═══════════════════════════════════════════════════════════════════════════

public interface IAGOnePermissionService
{
    bool HasPermission(string code);
    bool HasAll(params string[] codes);
    bool HasAny(params string[] codes);
    HashSet<string> GetPermissions();
    List<string> GetRoles();
    List<string> GetProductRoles();
    string? GetPrimaryRole();
    Guid GetUserId();
    Guid GetTenantId();
    Guid? GetProductId();
}

// ═══════════════════════════════════════════════════════════════════════════
// 3. CLIENT IMPLEMENTATION — for Blazor UI (reads from AuthenticationStateProvider)
// ═══════════════════════════════════════════════════════════════════════════

public sealed class ClientPermissionService : IAGOnePermissionService
{
    private readonly AuthenticationStateProvider _authState;
    private HashSet<string>? _cachedPermissions;
    private ClaimsPrincipal? _cachedUser;

    public ClientPermissionService(AuthenticationStateProvider authState)
    {
        _authState = authState;
    }

    private ClaimsPrincipal GetUser()
    {
        if (_cachedUser != null) return _cachedUser;
        var state = _authState.GetAuthenticationStateAsync().GetAwaiter().GetResult();
        _cachedUser = state.User;
        return _cachedUser;
    }

    public Guid GetUserId()
    {
        var v = GetUser().FindFirst("sub")?.Value;
        return Guid.TryParse(v, out var id) ? id : throw new UnauthorizedAccessException("No sub claim");
    }

    public Guid GetTenantId()
    {
        var v = GetUser().FindFirst("tenant_id")?.Value;
        return Guid.TryParse(v, out var id) ? id : throw new UnauthorizedAccessException("No tenant_id claim");
    }

    public Guid? GetProductId()
    {
        var v = GetUser().FindFirst("product_id")?.Value;
        return Guid.TryParse(v, out var id) ? id : null;
    }

    public string? GetPrimaryRole() => GetUser().FindFirst("primary_role")?.Value;
    public List<string> GetRoles() => GetUser().FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
    public List<string> GetProductRoles() => GetUser().FindAll("product_role").Select(c => c.Value).ToList();

    public HashSet<string> GetPermissions()
    {
        if (_cachedPermissions != null) return _cachedPermissions;
        _cachedPermissions = GetUser().FindAll("permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _cachedPermissions;
    }

    public bool HasPermission(string code) => GetPermissions().Contains(code);
    public bool HasAll(params string[] codes) { var p = GetPermissions(); return codes.All(p.Contains); }
    public bool HasAny(params string[] codes) { var p = GetPermissions(); return codes.Any(p.Contains); }
}

// ═══════════════════════════════════════════════════════════════════════════
// 4. DI — call from Blazor UI Program.cs
// ═══════════════════════════════════════════════════════════════════════════

public static class AGOnePermissionExtensions
{
    public static IServiceCollection AddAGOneClientPermissions(this IServiceCollection services)
    {
        services.AddScoped<IAGOnePermissionService, ClientPermissionService>();
        return services;
    }
}

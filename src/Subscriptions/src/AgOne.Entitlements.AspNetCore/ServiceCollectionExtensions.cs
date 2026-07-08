using AgOne.Entitlements.Abstractions;
using AgOne.Entitlements.AspNetCore.Authorization;
using AgOne.Entitlements.Catalog;
using AgOne.Entitlements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgOne.Entitlements.AspNetCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the entitlement engine + ASP.NET Core authorization integration.
    /// Caller must also register their own <see cref="ISubscriptionStore"/> and
    /// <see cref="IUserPermissionProvider"/> (the adapters over billing.* and core.*).
    /// </summary>
    public static IServiceCollection AddAgOneEntitlements(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<ICatalogProvider, PlanCatalog>();

        // Snapshot engine, wrapped in the caching decorator.
        services.AddScoped<EntitlementService>();
        services.AddScoped<IEntitlementService>(sp => new CachingEntitlementService(
            sp.GetRequiredService<EntitlementService>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));

        services.AddScoped<IAccessResolver, AccessResolver>();
        services.AddScoped<AgOne.Entitlements.Gateway.IGatewayEntitlementService,
                           AgOne.Entitlements.Gateway.GatewayEntitlementService>();

        // Request-scoped tenant/user context.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        // Dynamic policies + handler.
        services.AddSingleton<IAuthorizationPolicyProvider, EntitlementPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, EntitlementAuthorizationHandler>();
        services.AddAuthorization();

        return services;
    }

    /// <summary>Populates the tenant context from the authenticated principal. Call after UseAuthentication.</summary>
    public static IApplicationBuilder UseAgOneTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}

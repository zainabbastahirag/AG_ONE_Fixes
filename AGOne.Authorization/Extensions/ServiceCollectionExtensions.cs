using System.Text;
using AGOne.Authorization.Entities;
using AGOne.Authorization.Handlers;
using AGOne.Authorization.Models;
using AGOne.Authorization.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AGOne.Authorization.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register AG ONE Authorization for the GATEWAY (AG ONE main app).
    /// Reads permissions from EF Core database, issues JWT tokens.
    /// 
    /// Usage in AG ONE's Program.cs:
    ///   builder.Services.AddAGOneGatewayAuthorization(options => {
    ///       options.JwtSecret = "your-256-bit-secret";
    ///       options.CacheDuration = TimeSpan.FromMinutes(5);
    ///   }, dbOptions => dbOptions.UseSqlServer(connectionString));
    /// </summary>
    public static IServiceCollection AddAGOneGatewayAuthorization(
        this IServiceCollection services,
        Action<AGOneAuthOptions> configureOptions,
        Action<DbContextOptionsBuilder> configureDb)
    {
        var options = new AGOneAuthOptions { IsGateway = true };
        configureOptions(options);
        services.AddSingleton(options);

        services.AddDbContext<AuthorizationDbContext>(configureDb);

        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddScoped<ICurrentUserAccessor, HttpContextUserAccessor>();
        services.AddScoped<IPermissionService, GatewayPermissionService>();
        services.AddSingleton<IJwtPermissionTokenService, JwtPermissionTokenService>();

        AddSharedAuthorizationInfrastructure(services, options);

        return services;
    }

    /// <summary>
    /// Register AG ONE Authorization for a DOWNSTREAM product (Work, Learn, Safe, Pulse, Spot).
    /// Reads permissions from JWT claims, refreshes from gateway when version changes.
    /// 
    /// Usage in AG ONE Work's Program.cs:
    ///   builder.Services.AddAGOneProductAuthorization(options => {
    ///       options.GatewayBaseUrl = "https://agone-gateway.example.com";
    ///       options.JwtSecret = "your-256-bit-secret";  // same key as gateway
    ///       options.CacheDuration = TimeSpan.FromMinutes(5);
    ///   });
    /// </summary>
    public static IServiceCollection AddAGOneProductAuthorization(
        this IServiceCollection services,
        Action<AGOneAuthOptions> configureOptions)
    {
        var options = new AGOneAuthOptions { IsGateway = false };
        configureOptions(options);
        services.AddSingleton(options);

        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddHttpClient("AGOneGateway", client =>
        {
            client.BaseAddress = new Uri(options.GatewayBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddScoped<ICurrentUserAccessor, HttpContextUserAccessor>();
        services.AddScoped<IPermissionService, DownstreamPermissionService>();

        AddSharedAuthorizationInfrastructure(services, options);

        return services;
    }

    private static void AddSharedAuthorizationInfrastructure(IServiceCollection services, AGOneAuthOptions options)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = options.JwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSecret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                jwt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var cookie = context.Request.Cookies["agone_token"];
                        if (!string.IsNullOrEmpty(cookie))
                            context.Token = cookie;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionRequirementHandler>();
    }
}

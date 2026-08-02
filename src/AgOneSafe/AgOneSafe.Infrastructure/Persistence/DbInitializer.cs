using AgOneSafe.Application.Tenants;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Infrastructure.Catalog;
using AgOneSafe.Infrastructure.Emulation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgOneSafe.Infrastructure.Persistence;

/// <summary>
/// Brings a fresh checkout to a demonstrable state: schema, roles, the three persona logins,
/// the benchmark catalog and one connected tenant running against the emulator.
/// </summary>
public static class DbInitializer
{
    private const string DemoPassword = "AgOne!Safe2026";

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var db = provider.GetRequiredService<AgOneSafeDbContext>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        await db.Database.EnsureCreatedAsync(cancellationToken);

        await SeedRolesAndUsersAsync(provider, cancellationToken);

        var contentRoot = provider.GetRequiredService<IHostEnvironment>().ContentRootPath;
        var configured = configuration["Catalog:Directory"] ?? Path.Combine("App_Data", "catalog");
        var catalogDirectory = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(contentRoot, configured));

        var loader = provider.GetRequiredService<ICatalogLoader>();
        var loaded = await loader.LoadFromDirectoryAsync(catalogDirectory, cancellationToken);
        logger.LogInformation("Control catalog contains {Count} controls", loaded);

        await SeedTenantAsync(provider, db, cancellationToken);
    }

    private static async Task SeedRolesAndUsersAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in AgRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "ciso@aventra.example", "Dana Patel", "Chief Information Security Officer", AgRoles.Ciso);
        await EnsureUserAsync(userManager, "admin@aventra.example", "Sam Okafor", "Cloud Security Engineer", AgRoles.SecurityAdmin);
        await EnsureUserAsync(userManager, "auditor@aventra.example", "Ida Lindqvist", "Compliance Auditor", AgRoles.Auditor);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string displayName,
        string jobTitle,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                JobTitle = jobTitle
            };

            var result = await userManager.CreateAsync(user, DemoPassword);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the {role} demo account: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

    private static async Task SeedTenantAsync(
        IServiceProvider provider,
        AgOneSafeDbContext db,
        CancellationToken cancellationToken)
    {
        if (await db.TenantConnections.AnyAsync(cancellationToken))
        {
            return;
        }

        var tenants = provider.GetRequiredService<ITenantConnectionService>();

        var tenant = await tenants.OnboardAsync(new TenantOnboardingRequest
        {
            DisplayName = "Contoso Group",
            TenantId = "8f4c2b7e-0a91-4a3d-9d1e-5c6b7a8d9e01",
            PrimaryDomain = "contoso.com",
            ClientId = "1b9a5f30-6c2e-4f18-b0d5-2a3c4e5f6a7b",
            SecretReference = "kv-agone-prod/secrets/contoso-app-secret",
            AzureSubscriptionIds = "00000000-0000-0000-0000-000000000000",
            LighthouseEnabled = true,
            LicenseSkus = "Microsoft 365 E3, Microsoft Entra ID P1",
            TargetLevel = ProfileLevel.L1,
            ExecutionMode = ExecutionMode.Simulation,
            RequestedBy = "admin@aventra.example"
        }, cancellationToken);

        var emulator = provider.GetRequiredService<ITenantEmulator>();
        await emulator.EnsureStateAsync(tenant, cancellationToken);
    }
}

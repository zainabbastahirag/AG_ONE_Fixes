using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Agent;
using AgOneSafe.Application.Assessments;
using AgOneSafe.Application.Escalations;
using AgOneSafe.Application.Evaluation;
using AgOneSafe.Application.Remediation;
using AgOneSafe.Application.Tenants;
using AgOneSafe.Infrastructure.Catalog;
using AgOneSafe.Infrastructure.Emulation;
using AgOneSafe.Infrastructure.Execution;
using AgOneSafe.Infrastructure.Persistence;
using AgOneSafe.Infrastructure.Procurement;
using AgOneSafe.Infrastructure.Security;
using AgOneSafe.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgOneSafe.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the whole engine. <paramref name="contentRootPath"/> anchors the relative paths in
    /// configuration - the SQLite file, the catalog packs and the emulator state all live under the
    /// content root so they survive a rebuild that wipes bin/.
    /// </summary>
    public static IServiceCollection AddAgOneSafe(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        services.Configure<ExecutorOptions>(configuration.GetSection(ExecutorOptions.SectionName));
        services.PostConfigure<ExecutorOptions>(options =>
        {
            options.Emulator.StateDirectory = Resolve(contentRootPath, options.Emulator.StateDirectory);
            options.Emulator.ModulePath = Resolve(contentRootPath, options.Emulator.ModulePath);
            options.PowerShell.ScriptDirectory = Resolve(contentRootPath, options.PowerShell.ScriptDirectory);
        });

        services.Configure<Pax8Options>(configuration.GetSection(Pax8Options.SectionName));
        services.Configure<AgentPolicyOptions>(configuration.GetSection("AgentPolicy"));

        var connectionString = ResolveConnectionString(
            configuration.GetConnectionString("DefaultConnection") ?? "Data Source=App_Data/agonesafe.db",
            contentRootPath);

        services.AddDbContext<AgOneSafeDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IAgOneSafeDbContext>(provider => provider.GetRequiredService<AgOneSafeDbContext>());

        services.AddHttpClient("graph", client =>
            client.BaseAddress = new Uri(configuration["Executors:Graph:BaseAddress"] ?? "https://graph.microsoft.com"));

        services.AddHttpClient("arm", client =>
            client.BaseAddress = new Uri(
                configuration["Executors:AzureResourceManager:BaseAddress"] ?? "https://management.azure.com"));

        services.AddSingleton<ITenantEmulator, TenantEmulator>();
        services.AddSingleton<IPayloadRenderer, PayloadRenderer>();
        services.AddSingleton<IAssertionEvaluator, AssertionEvaluator>();
        services.AddSingleton<IBlastRadiusAnalyzer, BlastRadiusAnalyzer>();

        services.AddSingleton<ISecretStore, ConfigurationSecretStore>();
        services.AddSingleton<ITokenProvider, ClientCredentialsTokenProvider>();

        // Every executor registered here becomes immediately addressable from the catalog.
        services.AddSingleton<IControlExecutor, PowerShellExecutor>();
        services.AddSingleton<IControlExecutor, MicrosoftGraphExecutor>();
        services.AddSingleton<IControlExecutor, AzureRestExecutor>();
        services.AddSingleton<IControlExecutor, AzureResourceGraphExecutor>();
        services.AddSingleton<IControlExecutor, ManualExecutor>();
        services.AddSingleton<IExecutorRegistry, ExecutorRegistry>();

        services.AddScoped<IAuditTrail, HashChainedAuditTrail>();
        services.AddScoped<ICatalogLoader, CatalogLoader>();
        services.AddScoped<IExcelCatalogImporter, ExcelCatalogImporter>();

        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IPostureService, PostureService>();
        services.AddScoped<IRoadmapService, RoadmapService>();
        services.AddScoped<IRemediationAgent, PolicyDrivenRemediationAgent>();
        services.AddScoped<IRemediationService, RemediationService>();
        services.AddScoped<ITenantConnectionService, TenantConnectionService>();
        services.AddScoped<IProcurementClient, Pax8ProcurementClient>();
        services.AddScoped<IEscalationService, EscalationService>();

        services.AddSingleton<IScanQueue, ScanQueue>();
        services.AddHostedService<ScanWorker>();
        services.AddHostedService<MaintenanceWindowWorker>();

        return services;
    }

    private static string Resolve(string root, string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(root, path));

    private static string ResolveConnectionString(string connectionString, string root)
    {
        const string prefix = "Data Source=";

        var index = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return connectionString;
        }

        var start = index + prefix.Length;
        var end = connectionString.IndexOf(';', start);
        var value = end < 0 ? connectionString[start..] : connectionString[start..end];

        if (Path.IsPathRooted(value))
        {
            return connectionString;
        }

        var resolved = Resolve(root, value);
        Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);

        return connectionString.Remove(start, value.Length).Insert(start, resolved);
    }
}

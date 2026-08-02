using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgOneSafe.Domain.Tenants;
using AgOneSafe.Infrastructure.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgOneSafe.Infrastructure.Emulation;

/// <summary>
/// Backs Simulation mode with a mutable per-tenant state document, so a scan, a fix, a verification
/// re-scan and a rollback all act on the same data and genuinely change each other's results.
/// </summary>
public interface ITenantEmulator
{
    /// <summary>Creates the tenant's state document from the seed template if it does not exist yet.</summary>
    Task EnsureStateAsync(TenantConnection tenant, CancellationToken cancellationToken = default);

    Task<string> BuildPowerShellPrologueAsync(TenantConnection tenant, CancellationToken cancellationToken = default);

    /// <summary>Reads a top-level key, used by the simulated Graph and ARM executors.</summary>
    Task<JsonNode?> ReadAsync(TenantConnection tenant, string key, CancellationToken cancellationToken = default);

    Task WriteAsync(TenantConnection tenant, string key, JsonNode value, CancellationToken cancellationToken = default);

    string GetStatePath(TenantConnection tenant);

    Task ResetAsync(TenantConnection tenant, CancellationToken cancellationToken = default);
}

public sealed class TenantEmulator : ITenantEmulator
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    private readonly EmulatorOptions _options;
    private readonly ILogger<TenantEmulator> _logger;

    public TenantEmulator(IOptions<ExecutorOptions> options, ILogger<TenantEmulator> logger)
    {
        _options = options.Value.Emulator;
        _logger = logger;
    }

    public string GetStatePath(TenantConnection tenant) =>
        Path.Combine(_options.StateDirectory, $"{Sanitize(tenant.TenantId)}.json");

    private string SeedPath => Path.Combine(_options.StateDirectory, "tenant-seed.json");

    private string ModulePath => _options.ModulePath;

    public async Task EnsureStateAsync(TenantConnection tenant, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.StateDirectory);

        var statePath = GetStatePath(tenant);
        if (File.Exists(statePath))
        {
            return;
        }

        if (!File.Exists(SeedPath))
        {
            throw new FileNotFoundException(
                $"Tenant emulator seed '{SeedPath}' is missing. It ships with the application under App_Data/emulator.",
                SeedPath);
        }

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(statePath))
            {
                File.Copy(SeedPath, statePath);
                _logger.LogInformation("Created emulator state for tenant {Tenant} at {Path}", tenant.TenantId, statePath);
            }
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task ResetAsync(TenantConnection tenant, CancellationToken cancellationToken = default)
    {
        var statePath = GetStatePath(tenant);

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }
        }
        finally
        {
            FileLock.Release();
        }

        await EnsureStateAsync(tenant, cancellationToken);
    }

    public async Task<string> BuildPowerShellPrologueAsync(
        TenantConnection tenant,
        CancellationToken cancellationToken = default)
    {
        await EnsureStateAsync(tenant, cancellationToken);

        if (!File.Exists(ModulePath))
        {
            throw new FileNotFoundException(
                $"Tenant emulator module '{ModulePath}' is missing.", ModulePath);
        }

        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("$ProgressPreference = 'SilentlyContinue'");
        builder.AppendLine($"Import-Module '{Escape(ModulePath)}' -Force");
        builder.AppendLine($"Initialize-AgTenantEmulator -StatePath '{Escape(GetStatePath(tenant))}'");

        return builder.ToString();
    }

    public async Task<JsonNode?> ReadAsync(
        TenantConnection tenant,
        string key,
        CancellationToken cancellationToken = default)
    {
        await EnsureStateAsync(tenant, cancellationToken);

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            var root = await LoadAsync(GetStatePath(tenant), cancellationToken);
            return root?[key]?.DeepClone();
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task WriteAsync(
        TenantConnection tenant,
        string key,
        JsonNode value,
        CancellationToken cancellationToken = default)
    {
        await EnsureStateAsync(tenant, cancellationToken);

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            var path = GetStatePath(tenant);
            var root = await LoadAsync(path, cancellationToken) ?? new JsonObject();
            root[key] = value.DeepClone();

            await File.WriteAllTextAsync(
                path,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private static async Task<JsonObject?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonNode.Parse(json) as JsonObject;
    }

    private static string Sanitize(string value) =>
        new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());

    private static string Escape(string path) => path.Replace("'", "''", StringComparison.Ordinal);
}

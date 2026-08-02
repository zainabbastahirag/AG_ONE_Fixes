using System.Text.Json;
using System.Text.Json.Nodes;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Infrastructure.Execution;
using AgOneSafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgOneSafe.Infrastructure.Catalog;

public interface ICatalogLoader
{
    /// <summary>Upserts every benchmark pack found in the catalog directory and returns the row count.</summary>
    Task<int> LoadFromDirectoryAsync(string directory, CancellationToken cancellationToken = default);

    Task<int> UpsertAsync(CatalogDocument document, CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns benchmark packs into catalog rows. The upsert is keyed on (benchmark, vendor reference)
/// so re-running it after a workbook update is safe and preserves findings history.
/// </summary>
public sealed class CatalogLoader : ICatalogLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly AgOneSafeDbContext _db;
    private readonly EmulatorOptions _emulatorOptions;
    private readonly ILogger<CatalogLoader> _logger;

    public CatalogLoader(AgOneSafeDbContext db, IOptions<ExecutorOptions> options, ILogger<CatalogLoader> logger)
    {
        _db = db;
        _emulatorOptions = options.Value.Emulator;
        _logger = logger;
    }

    public async Task<int> LoadFromDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Catalog directory {Directory} does not exist", directory);
            return 0;
        }

        var total = 0;
        var seedState = new JsonObject();

        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(f => f))
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var document = JsonSerializer.Deserialize<CatalogDocument>(json, SerializerOptions);

            if (document is null)
            {
                _logger.LogWarning("Catalog pack {File} could not be parsed", file);
                continue;
            }

            total += await UpsertAsync(document, cancellationToken);

            foreach (var control in document.Controls.Where(c => c.EmulatorState is not null))
            {
                Merge(seedState, control.EmulatorState!);
            }
        }

        await WriteEmulatorSeedAsync(seedState, cancellationToken);

        _logger.LogInformation("Catalog loaded: {Count} controls across {Files} packs", total,
            Directory.EnumerateFiles(directory, "*.json").Count());

        return total;
    }

    public async Task<int> UpsertAsync(CatalogDocument document, CancellationToken cancellationToken = default)
    {
        var benchmark = Parse<BenchmarkFamily>(document.Benchmark, BenchmarkFamily.Microsoft365);

        var references = document.Controls.Select(c => c.Reference).ToList();

        var existing = await _db.ControlDefinitions
            .Include(c => c.Actions)
            .ThenInclude(a => a.Assertions)
            .Where(c => c.Benchmark == benchmark && references.Contains(c.VendorReference))
            .ToListAsync(cancellationToken);

        foreach (var source in document.Controls)
        {
            var control = existing.FirstOrDefault(c => c.VendorReference == source.Reference);

            if (control is null)
            {
                control = new ControlDefinition
                {
                    VendorReference = source.Reference,
                    Benchmark = benchmark
                };
                _db.ControlDefinitions.Add(control);
            }
            else
            {
                // Actions are rebuilt wholesale: a benchmark revision can change the payload,
                // the assertions or both, and partial merges are the classic source of drift.
                _db.ControlActions.RemoveRange(control.Actions);
                control.Actions.Clear();
            }

            control.BenchmarkVersion = document.Version;
            control.Domain = source.Domain;
            control.Title = source.Title;
            control.Description = source.Description;
            control.Rationale = source.Rationale;
            control.Level = Parse(source.Level, ProfileLevel.L1);
            control.Severity = Parse(source.Severity, Severity.Medium);
            control.UserImpact = source.UserImpact;
            control.CisCriticalSecurityControl = source.CisControl;
            control.NistCsfFunction = Parse(source.NistFunction, NistCsfFunction.Protect);
            control.NistCsfCategory = source.NistCategory;
            control.AuditProcedure = source.AuditProcedure;
            control.RemediationProcedure = source.RemediationProcedure;
            control.RequiredLicense = string.IsNullOrWhiteSpace(source.RequiredLicense) ? null : source.RequiredLicense;
            control.RiskWeight = source.RiskWeight;
            control.EffortWeight = source.EffortWeight;
            control.IsAutomatedAudit = source.AutomatedAudit;
            control.IsAutomatedRemediation = source.AutomatedRemediation && source.Remediation is not null;
            control.IsEnabled = true;
            control.UpdatedAt = DateTimeOffset.UtcNow;

            AddAction(control, source.Audit, ControlActionKind.Audit);
            AddAction(control, source.Remediation, ControlActionKind.Remediate);
            AddAction(control, source.Rollback, ControlActionKind.Rollback);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return document.Controls.Count;
    }

    private static void AddAction(ControlDefinition control, CatalogAction? source, ControlActionKind kind)
    {
        if (source is null)
        {
            return;
        }

        var action = new ControlAction
        {
            Kind = kind,
            Name = string.IsNullOrWhiteSpace(source.Name) ? $"{kind} {control.VendorReference}" : source.Name,
            ExecutorType = Parse(source.Executor, ExecutorType.PowerShell),
            Module = Parse(source.Module, ConnectorModule.MicrosoftGraph),
            Payload = source.Payload,
            DryRunPayload = source.DryRunPayload,
            ParametersJson = source.Parameters?.ToJsonString() ?? "{}",
            RequiredScopes = source.Scopes,
            TimeoutSeconds = source.TimeoutSeconds,
            IsDestructive = source.Destructive,
            SupportsDryRun = !string.IsNullOrWhiteSpace(source.DryRunPayload) || kind == ControlActionKind.Audit,
            AssertionLogic = Parse(source.Logic, AssertionLogic.All)
        };

        var sequence = 0;
        foreach (var assertion in source.Assertions)
        {
            action.Assertions.Add(new AssertionRule
            {
                Sequence = sequence++,
                JsonPath = assertion.Path,
                Operator = Parse(assertion.Operator, AssertionOperator.Equals),
                ExpectedValue = assertion.Expected,
                SecondaryValue = assertion.Secondary,
                CaseSensitive = assertion.CaseSensitive,
                FailureMessage = assertion.Failure,
                RemediationHint = assertion.Hint
            });
        }

        control.Actions.Add(action);
    }

    private async Task WriteEmulatorSeedAsync(JsonObject seedState, CancellationToken cancellationToken)
    {
        if (seedState.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(_emulatorOptions.StateDirectory);
        var path = Path.Combine(_emulatorOptions.StateDirectory, "tenant-seed.json");

        await File.WriteAllTextAsync(
            path,
            seedState.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        _logger.LogInformation("Emulator seed written to {Path} with {Count} state keys", path, seedState.Count);
    }

    /// <summary>
    /// Deep-merges one control's emulator fixture into the shared seed. Several controls read the
    /// same cmdlet (Get-OrganizationConfig backs four of them), so objects merge property by
    /// property instead of the last pack overwriting the others' fields.
    /// </summary>
    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var entry in source)
        {
            if (entry.Value is JsonObject nested &&
                target[entry.Key] is JsonObject existing)
            {
                Merge(existing, nested);
                continue;
            }

            target[entry.Key] = entry.Value?.DeepClone();
        }
    }

    private static TEnum Parse<TEnum>(string? value, TEnum fallback) where TEnum : struct =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}

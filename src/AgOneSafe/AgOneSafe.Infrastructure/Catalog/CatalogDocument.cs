using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgOneSafe.Infrastructure.Catalog;

/// <summary>
/// On-disk shape of a benchmark pack. One file per benchmark section keeps the catalog reviewable
/// in pull requests; the loader upserts it into SQL, which is what the engine actually reads.
/// </summary>
public sealed class CatalogDocument
{
    public string Benchmark { get; set; } = "Microsoft365";
    public string Version { get; set; } = string.Empty;
    public List<CatalogControl> Controls { get; set; } = new();
}

public sealed class CatalogControl
{
    public string Reference { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;

    public string Level { get; set; } = "L1";
    public string Severity { get; set; } = "Medium";
    public string UserImpact { get; set; } = "No Impact";

    public string CisControl { get; set; } = string.Empty;
    public string NistFunction { get; set; } = "Protect";
    public string NistCategory { get; set; } = string.Empty;

    /// <summary>Verbatim "Audit by" text from the workbook.</summary>
    public string AuditProcedure { get; set; } = string.Empty;

    /// <summary>Verbatim "Remediation" text from the workbook.</summary>
    public string RemediationProcedure { get; set; } = string.Empty;

    public string? RequiredLicense { get; set; }

    public int RiskWeight { get; set; } = 5;
    public int EffortWeight { get; set; } = 1;

    public bool AutomatedAudit { get; set; } = true;
    public bool AutomatedRemediation { get; set; } = true;

    public CatalogAction? Audit { get; set; }
    public CatalogAction? Remediation { get; set; }
    public CatalogAction? Rollback { get; set; }

    /// <summary>
    /// Demo tenant state this control reads and writes. The loader merges every control's block
    /// into the emulator seed, so a control and its Simulation-mode fixture stay together.
    /// </summary>
    public JsonObject? EmulatorState { get; set; }
}

public sealed class CatalogAction
{
    public string Name { get; set; } = string.Empty;
    public string Executor { get; set; } = "PowerShell";
    public string Module { get; set; } = "MicrosoftGraph";
    public string Payload { get; set; } = string.Empty;
    public string? DryRunPayload { get; set; }
    public string Scopes { get; set; } = string.Empty;
    public string Logic { get; set; } = "All";
    public bool Destructive { get; set; }
    public int TimeoutSeconds { get; set; } = 120;

    [JsonPropertyName("parameters")]
    public JsonObject? Parameters { get; set; }

    public List<CatalogAssertion> Assertions { get; set; } = new();
}

public sealed class CatalogAssertion
{
    public string Path { get; set; } = string.Empty;
    public string Operator { get; set; } = "Equals";
    public string? Expected { get; set; }
    public string? Secondary { get; set; }
    public bool CaseSensitive { get; set; }

    /// <summary>Why the control failed, with {actual}/{expected}/{count} placeholders.</summary>
    public string Failure { get; set; } = string.Empty;

    /// <summary>What has to change to close the gap.</summary>
    public string Hint { get; set; } = string.Empty;
}

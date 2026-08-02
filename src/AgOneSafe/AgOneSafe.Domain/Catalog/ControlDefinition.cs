using System.ComponentModel.DataAnnotations;

namespace AgOneSafe.Domain.Catalog;

/// <summary>
/// One row of the CIS benchmark workbook, stored in SQL. Every column of the customer's
/// "Vendor Reference / Level / Title / Audit by / Remediation / User Impact / CIS Control /
/// NIST CSF Core Area" spreadsheet maps onto this entity plus its <see cref="Actions"/>.
/// Nothing about a control is compiled into the product: the engine reads this table at runtime.
/// </summary>
public class ControlDefinition
{
    public int Id { get; set; }

    /// <summary>Benchmark identifier as printed in the workbook, e.g. "2.1.1" or "3.1".</summary>
    [MaxLength(32)]
    public string VendorReference { get; set; } = string.Empty;

    public BenchmarkFamily Benchmark { get; set; }

    [MaxLength(32)]
    public string BenchmarkVersion { get; set; } = string.Empty;

    /// <summary>Posture domain used for scoring breakdown, e.g. "Identity", "Exchange Online", "Storage".</summary>
    [MaxLength(64)]
    public string Domain { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Why the control matters - shown to the CISO on the finding detail page.</summary>
    public string Rationale { get; set; } = string.Empty;

    /// <summary>
    /// Verbatim "Audit by" column from the benchmark workbook. Shown to analysts exactly as the
    /// CIS author wrote it, including portal click-paths, while <see cref="ControlAction.Payload"/>
    /// holds the machine-executable form of the same check.
    /// </summary>
    public string AuditProcedure { get; set; } = string.Empty;

    /// <summary>Verbatim "Remediation" column from the workbook.</summary>
    public string RemediationProcedure { get; set; } = string.Empty;

    public ProfileLevel Level { get; set; } = ProfileLevel.L1;

    public Severity Severity { get; set; } = Severity.Medium;

    /// <summary>"User impact" workbook column - surfaced in the blast-radius preview.</summary>
    public string UserImpact { get; set; } = "No Impact";

    /// <summary>"CIS Critical Security Control" workbook column, e.g. "CIS Control 6: Access Control Management".</summary>
    [MaxLength(128)]
    public string CisCriticalSecurityControl { get; set; } = string.Empty;

    /// <summary>"NIST CSF Core Area" workbook column.</summary>
    public NistCsfFunction NistCsfFunction { get; set; } = NistCsfFunction.Protect;

    [MaxLength(128)]
    public string NistCsfCategory { get; set; } = string.Empty;

    /// <summary>False when the control can only be checked by a human walking the admin portal.</summary>
    public bool IsAutomatedAudit { get; set; } = true;

    /// <summary>False when remediation needs a human (procurement, org process, portal-only toggle).</summary>
    public bool IsAutomatedRemediation { get; set; } = true;

    /// <summary>
    /// Set when the fix cannot be applied on the tenant's current SKU, e.g. "Microsoft Entra ID P2".
    /// Drives the license-gap escalation workflow.
    /// </summary>
    [MaxLength(128)]
    public string? RequiredLicense { get; set; }

    /// <summary>Relative business risk weight (1-10) used to order the remediation roadmap.</summary>
    public int RiskWeight { get; set; } = 5;

    /// <summary>Rough engineering effort for the roadmap view: 1 = trivial toggle, 5 = project.</summary>
    public int EffortWeight { get; set; } = 1;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ControlAction> Actions { get; set; } = new();

    public ControlAction? AuditAction =>
        Actions.FirstOrDefault(a => a.Kind == ControlActionKind.Audit);

    public ControlAction? RemediationAction =>
        Actions.FirstOrDefault(a => a.Kind == ControlActionKind.Remediate);

    public ControlAction? RollbackAction =>
        Actions.FirstOrDefault(a => a.Kind == ControlActionKind.Rollback);

    public bool RequiresLicenseUpgrade => !string.IsNullOrWhiteSpace(RequiredLicense);

    public string DisplayName => $"{VendorReference} {Title}";
}

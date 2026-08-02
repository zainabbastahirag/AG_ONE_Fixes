using System.ComponentModel.DataAnnotations;

namespace AgOneSafe.Domain.Catalog;

public enum ControlActionKind
{
    /// <summary>The "Audit by" workbook column - reads tenant state and produces evidence.</summary>
    Audit = 0,

    /// <summary>The "Remediation" workbook column - changes tenant state.</summary>
    Remediate = 1,

    /// <summary>Restores the state captured in the pre-execution snapshot.</summary>
    Rollback = 2,

    /// <summary>Reads the state that must be captured before remediation runs.</summary>
    Snapshot = 3
}

/// <summary>
/// A single executable step attached to a control. This is the unit the generic engine runs:
/// pick an executor, hand it <see cref="Payload"/> plus <see cref="ParametersJson"/>, get JSON back,
/// then let the assertion rules decide pass/fail. Adding a new control is a data insert, not a code change.
/// </summary>
public class ControlAction
{
    public int Id { get; set; }

    public int ControlDefinitionId { get; set; }
    public ControlDefinition? ControlDefinition { get; set; }

    public ControlActionKind Kind { get; set; }

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    public ExecutorType ExecutorType { get; set; } = ExecutorType.PowerShell;

    /// <summary>Workload session the executor must establish first (Exchange Online, Teams, Graph...).</summary>
    public ConnectorModule Module { get; set; } = ConnectorModule.MicrosoftGraph;

    /// <summary>
    /// The executable body. PowerShell script text, a Graph/ARM request descriptor in JSON,
    /// or human instructions for <see cref="ExecutorType.Manual"/>.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Optional dry-run variant. When absent the engine falls back to a static preview so that
    /// nothing is written to the tenant during a rehearsal.
    /// </summary>
    public string? DryRunPayload { get; set; }

    /// <summary>JSON object of named parameters merged into the payload as $AgParam_&lt;name&gt;.</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>Graph/ARM permission scopes this action needs. Used by the permission health check.</summary>
    [MaxLength(512)]
    public string RequiredScopes { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>True when the action can lock users out or delete data, forcing a stricter approval tier.</summary>
    public bool IsDestructive { get; set; }

    public bool SupportsDryRun { get; set; } = true;

    /// <summary>Ordering when a control needs several steps of the same kind.</summary>
    public int Sequence { get; set; }

    public AssertionLogic AssertionLogic { get; set; } = AssertionLogic.All;

    public List<AssertionRule> Assertions { get; set; } = new();
}

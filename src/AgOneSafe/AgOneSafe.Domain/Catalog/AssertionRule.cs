using System.ComponentModel.DataAnnotations;

namespace AgOneSafe.Domain.Catalog;

/// <summary>
/// Turns raw executor output into a pass/fail verdict plus the human explanation of *why*
/// a control failed and *what* has to change. Stored per control so analysts can tune
/// thresholds without a redeploy.
/// </summary>
public class AssertionRule
{
    public int Id { get; set; }

    public int ControlActionId { get; set; }
    public ControlAction? ControlAction { get; set; }

    public int Sequence { get; set; }

    /// <summary>
    /// Dotted path into the JSON returned by the executor, e.g. "EnableSafeLinksForOffice",
    /// "value[0].state" or "policies[*].AllowGoogleDrive" ([*] projects across an array).
    /// </summary>
    [MaxLength(256)]
    public string JsonPath { get; set; } = string.Empty;

    public AssertionOperator Operator { get; set; } = AssertionOperator.Equals;

    /// <summary>Expected value, compared after normalising booleans and numbers.</summary>
    [MaxLength(1024)]
    public string? ExpectedValue { get; set; }

    /// <summary>Upper bound for range operators such as <see cref="AssertionOperator.Between"/>.</summary>
    [MaxLength(256)]
    public string? SecondaryValue { get; set; }

    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Why the control failed. Supports {actual}, {expected}, {path} and {count} placeholders,
    /// e.g. "Safe Links for Office applications is {actual}; CIS requires {expected}."
    /// </summary>
    [MaxLength(1024)]
    public string FailureMessage { get; set; } = string.Empty;

    /// <summary>The "what needs to be done" line shown in gap analysis next to the failure.</summary>
    [MaxLength(1024)]
    public string RemediationHint { get; set; } = string.Empty;
}

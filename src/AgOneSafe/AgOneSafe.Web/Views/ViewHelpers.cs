using AgOneSafe.Domain;

namespace AgOneSafe.Web.Views;

/// <summary>Shared presentation mappings so badge colours stay consistent across every screen.</summary>
public static class ViewHelpers
{
    public static string SeverityBadge(Severity severity) => severity switch
    {
        Severity.Critical => "badge-critical",
        Severity.High => "badge-high",
        Severity.Medium => "badge-medium",
        _ => "badge-low"
    };

    public static string OutcomeBadge(ControlOutcome outcome) => outcome switch
    {
        ControlOutcome.Pass => "badge-pass",
        ControlOutcome.Fail => "badge-fail",
        ControlOutcome.Error => "badge-high",
        ControlOutcome.Manual => "badge-info",
        _ => "badge-neutral"
    };

    public static string FindingBadge(FindingStatus status) => status switch
    {
        FindingStatus.Verified => "badge-pass",
        FindingStatus.Remediated => "badge-info",
        FindingStatus.VerificationFailed => "badge-critical",
        FindingStatus.BlockedByLicense => "badge-medium",
        FindingStatus.RiskAccepted => "badge-neutral",
        FindingStatus.ManualActionRequired => "badge-info",
        FindingStatus.Approved or FindingStatus.Scheduled or FindingStatus.InProgress => "badge-info",
        _ => "badge-fail"
    };

    public static string RemediationBadge(RemediationStatus status) => status switch
    {
        RemediationStatus.Succeeded => "badge-pass",
        RemediationStatus.Failed => "badge-critical",
        RemediationStatus.Rejected => "badge-critical",
        RemediationStatus.RolledBack => "badge-medium",
        RemediationStatus.PendingApproval => "badge-high",
        RemediationStatus.Approved or RemediationStatus.Scheduled => "badge-info",
        RemediationStatus.Running => "badge-info",
        _ => "badge-neutral"
    };

    public static string ScoreColour(double score) => score switch
    {
        >= 85 => "#35d19a",
        >= 65 => "#ffcc4d",
        >= 40 => "#ff9552",
        _ => "#ff5c72"
    };

    public static string LevelBadge(ProfileLevel level) => level == ProfileLevel.L1 ? "badge-l1" : "badge-l2";

    public static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var single = value.ReplaceLineEndings(" ").Trim();
        return single.Length <= max ? single : single[..max] + "…";
    }
}

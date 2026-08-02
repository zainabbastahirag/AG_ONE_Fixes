using AgOneSafe.Domain;

namespace AgOneSafe.Application.Evaluation;

/// <summary>Verdict for a single assertion, carrying the actual value so evidence is self-explaining.</summary>
public sealed class AssertionOutcome
{
    public int Sequence { get; init; }
    public string JsonPath { get; init; } = string.Empty;
    public AssertionOperator Operator { get; init; }
    public string? Expected { get; init; }
    public string? Actual { get; init; }
    public bool Passed { get; init; }

    /// <summary>Rendered failure message with {actual}/{expected} substituted.</summary>
    public string? Message { get; init; }

    public string? RemediationHint { get; init; }
}

/// <summary>Combined judgement for one control: the outcome plus the "why" and "what next".</summary>
public sealed class ControlEvaluation
{
    public ControlOutcome Outcome { get; init; }

    /// <summary>Newline-separated failure messages, empty when the control passed.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Newline-separated remediation hints for the gap analysis view.</summary>
    public string? RequiredAction { get; init; }

    public IReadOnlyList<AssertionOutcome> Assertions { get; init; } = Array.Empty<AssertionOutcome>();

    public string EvidenceJson { get; init; } = "{}";

    public string ExecutedCommand { get; init; } = string.Empty;

    public string? ExecutorError { get; init; }

    public int DurationMs { get; init; }
}

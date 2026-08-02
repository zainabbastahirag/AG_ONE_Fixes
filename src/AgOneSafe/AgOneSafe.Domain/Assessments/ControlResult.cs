using System.ComponentModel.DataAnnotations;
using AgOneSafe.Domain.Catalog;

namespace AgOneSafe.Domain.Assessments;

/// <summary>Outcome of evaluating one control during one assessment, with the evidence that proves it.</summary>
public class ControlResult
{
    public int Id { get; set; }

    public int AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }

    public int ControlDefinitionId { get; set; }
    public ControlDefinition? ControlDefinition { get; set; }

    public ControlOutcome Outcome { get; set; }

    /// <summary>Plain-language explanation of why the control failed, built from the assertion messages.</summary>
    [MaxLength(4000)]
    public string? FailureReason { get; set; }

    /// <summary>What has to change to close the gap - the roadmap's "action" column.</summary>
    [MaxLength(4000)]
    public string? RequiredAction { get; set; }

    /// <summary>Raw JSON the executor returned. Retained as audit evidence.</summary>
    public string EvidenceJson { get; set; } = "{}";

    /// <summary>Per-assertion detail (path, expected, actual, verdict) serialised as JSON.</summary>
    public string AssertionResultsJson { get; set; } = "[]";

    /// <summary>The exact command executed, kept so auditors can reproduce the check.</summary>
    public string ExecutedCommand { get; set; } = string.Empty;

    public int DurationMs { get; set; }

    public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(2048)]
    public string? ExecutorError { get; set; }

    public Finding? Finding { get; set; }
}

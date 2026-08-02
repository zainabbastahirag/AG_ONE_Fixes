using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;

namespace AgOneSafe.Application.Assessments;

public sealed class AssessmentRequest
{
    public required int TenantConnectionId { get; init; }

    public ProfileLevel? TargetLevel { get; init; }

    public AssessmentTrigger Trigger { get; init; } = AssessmentTrigger.Manual;

    public string TriggeredBy { get; init; } = "system";

    /// <summary>Restricts the scan to specific vendor references - used by post-remediation verification.</summary>
    public IReadOnlyCollection<string>? ControlReferences { get; init; }

    public BenchmarkFamily? Benchmark { get; init; }

    /// <summary>Number of controls evaluated concurrently. Tuned to hit the &lt;15 minute scan NFR.</summary>
    public int MaxConcurrency { get; init; } = 8;
}

public interface IAssessmentService
{
    Task<Assessment> RunAsync(AssessmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Weight a control carries in the posture score: risk weight scaled by severity.</summary>
    static double ControlWeight(Severity severity, int riskWeight) => Math.Max(1, riskWeight) * (int)severity;
}

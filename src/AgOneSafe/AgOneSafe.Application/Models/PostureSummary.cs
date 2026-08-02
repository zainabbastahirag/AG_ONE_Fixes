using AgOneSafe.Domain;

namespace AgOneSafe.Application.Models;

/// <summary>Dashboard roll-up for one tenant's most recent assessment.</summary>
public sealed class PostureSummary
{
    public int TenantConnectionId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public int? AssessmentId { get; init; }
    public DateTimeOffset? AssessedAt { get; init; }
    public ProfileLevel TargetLevel { get; init; }

    public double PostureScore { get; init; }
    public double PreviousPostureScore { get; init; }
    public double ScoreDelta => Math.Round(PostureScore - PreviousPostureScore, 1);

    public int TotalControls { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Errored { get; init; }
    public int Manual { get; init; }
    public int NotApplicable { get; init; }

    public int OpenFindings { get; init; }
    public int VerifiedFindings { get; init; }
    public int AwaitingApproval { get; init; }
    public int LicenseBlocked { get; init; }

    public Dictionary<Severity, int> BySeverity { get; init; } = new();
    public Dictionary<string, DomainScore> ByDomain { get; init; } = new();
    public Dictionary<NistCsfFunction, DomainScore> ByNistFunction { get; init; } = new();

    public IReadOnlyList<ScorePoint> Trend { get; init; } = Array.Empty<ScorePoint>();
}

public sealed class DomainScore
{
    public string Name { get; init; } = string.Empty;
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Total => Passed + Failed;
    public double Score => Total == 0 ? 100 : Math.Round(Passed * 100.0 / Total, 1);
}

public sealed record ScorePoint(DateTimeOffset At, double Score);

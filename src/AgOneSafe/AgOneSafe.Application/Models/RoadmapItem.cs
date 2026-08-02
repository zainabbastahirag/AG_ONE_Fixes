using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;

namespace AgOneSafe.Application.Models;

/// <summary>A prioritised entry in the gap-closure roadmap.</summary>
public sealed class RoadmapItem
{
    public required Finding Finding { get; init; }

    public string ControlReference { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public ProfileLevel Level { get; init; }
    public Severity Severity { get; init; }
    public NistCsfFunction NistFunction { get; init; }

    public double RiskScore { get; init; }

    /// <summary>Risk reduction per unit of effort - drives the ordering of the queue.</summary>
    public double PriorityScore { get; init; }

    public bool CanAutoRemediate { get; init; }
    public bool RequiresLicenseUpgrade { get; init; }
    public string? RequiredLicense { get; init; }

    /// <summary>Which delivery wave the item lands in: 1 = quick agentic wins, 3 = project work.</summary>
    public int Wave { get; init; }

    public string WaveName { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;
}

/// <summary>The roadmap for one tenant at a chosen target maturity.</summary>
public sealed class RemediationRoadmap
{
    public int TenantConnectionId { get; init; }
    public ProfileLevel TargetLevel { get; init; }

    public IReadOnlyList<RoadmapItem> Items { get; init; } = Array.Empty<RoadmapItem>();

    public double CurrentScore { get; init; }

    /// <summary>Score the tenant reaches if every auto-remediable item in the roadmap is executed.</summary>
    public double ProjectedScore { get; init; }

    public int AutoRemediableCount => Items.Count(i => i.CanAutoRemediate && !i.RequiresLicenseUpgrade);
    public int LicenseBlockedCount => Items.Count(i => i.RequiresLicenseUpgrade);
    public int ManualCount => Items.Count(i => !i.CanAutoRemediate && !i.RequiresLicenseUpgrade);

    public IEnumerable<IGrouping<int, RoadmapItem>> Waves => Items.GroupBy(i => i.Wave).OrderBy(g => g.Key);
}

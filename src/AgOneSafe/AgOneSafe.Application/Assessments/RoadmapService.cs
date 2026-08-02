using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Models;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Application.Assessments;

public interface IRoadmapService
{
    Task<RemediationRoadmap> BuildAsync(
        int tenantConnectionId,
        ProfileLevel targetLevel,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns open gaps into an ordered action plan. Items are ranked by risk reduction per unit of
/// effort so the first wave is what an agent can safely close on its own today.
/// </summary>
public sealed class RoadmapService : IRoadmapService
{
    private readonly IAgOneSafeDbContext _db;

    public RoadmapService(IAgOneSafeDbContext db) => _db = db;

    public async Task<RemediationRoadmap> BuildAsync(
        int tenantConnectionId,
        ProfileLevel targetLevel,
        CancellationToken cancellationToken = default)
    {
        var findings = await _db.Findings
            .AsNoTracking()
            .Include(f => f.ControlDefinition)
            .ThenInclude(c => c!.Actions)
            .Where(f => f.TenantConnectionId == tenantConnectionId &&
                        f.Status != FindingStatus.Verified &&
                        f.Status != FindingStatus.RiskAccepted)
            .ToListAsync(cancellationToken);

        var inScope = findings
            .Where(f => f.ControlDefinition is not null && f.ControlDefinition.Level <= targetLevel)
            .ToList();

        var items = inScope
            .Select(finding =>
            {
                var control = finding.ControlDefinition!;
                var canAuto = control.IsAutomatedRemediation &&
                              control.Actions.Any(a => a.Kind == Domain.Catalog.ControlActionKind.Remediate) &&
                              !control.RequiresLicenseUpgrade;

                var effort = Math.Clamp(control.EffortWeight, 1, 5);
                var priority = Math.Round(finding.RiskScore / effort, 2);
                var wave = DetermineWave(control.RequiresLicenseUpgrade, canAuto, finding.Severity);

                return new RoadmapItem
                {
                    Finding = finding,
                    ControlReference = control.VendorReference,
                    Title = control.Title,
                    Domain = control.Domain,
                    Level = control.Level,
                    Severity = finding.Severity,
                    NistFunction = control.NistCsfFunction,
                    RiskScore = finding.RiskScore,
                    PriorityScore = priority,
                    CanAutoRemediate = canAuto,
                    RequiresLicenseUpgrade = control.RequiresLicenseUpgrade,
                    RequiredLicense = control.RequiredLicense,
                    Wave = wave,
                    WaveName = WaveName(wave),
                    RecommendedAction = string.IsNullOrWhiteSpace(finding.RequiredAction)
                        ? control.RemediationAction?.Name ?? "Review the control guidance."
                        : finding.RequiredAction
                };
            })
            .OrderBy(i => i.Wave)
            .ThenByDescending(i => i.PriorityScore)
            .ThenByDescending(i => i.Severity)
            .ToList();

        var latest = await _db.Assessments
            .AsNoTracking()
            .Where(a => a.TenantConnectionId == tenantConnectionId &&
                        a.Status == AssessmentStatus.Completed &&
                        a.ScopedControlReferences == null)
            .OrderByDescending(a => a.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new RemediationRoadmap
        {
            TenantConnectionId = tenantConnectionId,
            TargetLevel = targetLevel,
            Items = items,
            CurrentScore = latest?.PostureScore ?? 0,
            ProjectedScore = await ProjectScoreAsync(tenantConnectionId, latest?.Id, items, cancellationToken)
        };
    }

    /// <summary>
    /// Recomputes the weighted score assuming every auto-remediable item in wave 1 and 2 passes.
    /// Gives the CISO a concrete "approve this and you land here" number.
    /// </summary>
    private async Task<double> ProjectScoreAsync(
        int tenantConnectionId,
        int? assessmentId,
        IReadOnlyList<RoadmapItem> items,
        CancellationToken cancellationToken)
    {
        if (assessmentId is null)
        {
            return 0;
        }

        var scored = await _db.ControlResults
            .AsNoTracking()
            .Where(r => r.AssessmentId == assessmentId &&
                        (r.Outcome == ControlOutcome.Pass || r.Outcome == ControlOutcome.Fail))
            .Select(r => new
            {
                r.Outcome,
                r.ControlDefinitionId,
                r.ControlDefinition!.Severity,
                r.ControlDefinition.RiskWeight
            })
            .ToListAsync(cancellationToken);

        if (scored.Count == 0)
        {
            return 0;
        }

        var fixable = items
            .Where(i => i.CanAutoRemediate)
            .Select(i => i.Finding.ControlDefinitionId)
            .ToHashSet();

        double total = 0;
        double passed = 0;

        foreach (var row in scored)
        {
            var weight = IAssessmentService.ControlWeight(row.Severity, row.RiskWeight);
            total += weight;

            if (row.Outcome == ControlOutcome.Pass || fixable.Contains(row.ControlDefinitionId))
            {
                passed += weight;
            }
        }

        return total <= 0 ? 0 : Math.Round(passed * 100.0 / total, 1);
    }

    private static int DetermineWave(bool licenseBlocked, bool canAuto, Severity severity)
    {
        if (licenseBlocked)
        {
            return 3;
        }

        if (!canAuto)
        {
            return 2;
        }

        return severity >= Severity.High ? 1 : 2;
    }

    private static string WaveName(int wave) => wave switch
    {
        1 => "Wave 1 - Agentic quick wins",
        2 => "Wave 2 - Assisted change",
        _ => "Wave 3 - Licence or services required"
    };
}

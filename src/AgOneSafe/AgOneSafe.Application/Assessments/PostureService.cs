using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Models;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Remediation;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Application.Assessments;

public interface IPostureService
{
    Task<PostureSummary> GetSummaryAsync(int tenantConnectionId, CancellationToken cancellationToken = default);
}

/// <summary>Builds the executive dashboard roll-up from the latest completed assessment.</summary>
public sealed class PostureService : IPostureService
{
    private readonly IAgOneSafeDbContext _db;

    public PostureService(IAgOneSafeDbContext db) => _db = db;

    public async Task<PostureSummary> GetSummaryAsync(int tenantConnectionId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.TenantConnections
                         .AsNoTracking()
                         .FirstOrDefaultAsync(t => t.Id == tenantConnectionId, cancellationToken)
                     ?? throw new InvalidOperationException($"Tenant {tenantConnectionId} not found.");

        var assessments = await _db.Assessments
            .AsNoTracking()
            .Where(a => a.TenantConnectionId == tenantConnectionId && a.Status == AssessmentStatus.Completed)
            .OrderByDescending(a => a.CompletedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        // Verification re-scans only touch a handful of controls, so they must not be mistaken
        // for a full baseline when reporting the headline score.
        var latest = assessments.FirstOrDefault(a => a.ScopedControlReferences == null);
        var previous = assessments.Where(a => a.ScopedControlReferences == null).Skip(1).FirstOrDefault();

        var findings = await _db.Findings
            .AsNoTracking()
            .Include(f => f.ControlDefinition)
            .Where(f => f.TenantConnectionId == tenantConnectionId)
            .ToListAsync(cancellationToken);

        var awaitingApproval = await _db.RemediationTasks
            .CountAsync(t => t.TenantConnectionId == tenantConnectionId &&
                             t.Status == RemediationStatus.PendingApproval, cancellationToken);

        var byDomain = new Dictionary<string, DomainScore>(StringComparer.OrdinalIgnoreCase);
        var byFunction = new Dictionary<NistCsfFunction, DomainScore>();

        if (latest is not null)
        {
            var rows = await _db.ControlResults
                .AsNoTracking()
                .Where(r => r.AssessmentId == latest.Id &&
                            (r.Outcome == ControlOutcome.Pass || r.Outcome == ControlOutcome.Fail))
                .Select(r => new
                {
                    r.Outcome,
                    r.ControlDefinition!.Domain,
                    r.ControlDefinition.NistCsfFunction
                })
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                if (!byDomain.TryGetValue(row.Domain, out var domainScore))
                {
                    domainScore = new DomainScore { Name = row.Domain };
                    byDomain[row.Domain] = domainScore;
                }

                if (!byFunction.TryGetValue(row.NistCsfFunction, out var functionScore))
                {
                    functionScore = new DomainScore { Name = row.NistCsfFunction.ToString() };
                    byFunction[row.NistCsfFunction] = functionScore;
                }

                if (row.Outcome == ControlOutcome.Pass)
                {
                    domainScore.Passed++;
                    functionScore.Passed++;
                }
                else
                {
                    domainScore.Failed++;
                    functionScore.Failed++;
                }
            }
        }

        var open = findings.Where(f => !f.IsClosed).ToList();

        return new PostureSummary
        {
            TenantConnectionId = tenantConnectionId,
            TenantName = tenant.DisplayName,
            AssessmentId = latest?.Id,
            AssessedAt = latest?.CompletedAt,
            TargetLevel = tenant.TargetLevel,
            PostureScore = latest?.PostureScore ?? 0,
            PreviousPostureScore = previous?.PostureScore ?? latest?.PostureScore ?? 0,
            TotalControls = latest?.TotalControls ?? 0,
            Passed = latest?.PassedCount ?? 0,
            Failed = latest?.FailedCount ?? 0,
            Errored = latest?.ErrorCount ?? 0,
            Manual = latest?.ManualCount ?? 0,
            NotApplicable = latest?.NotApplicableCount ?? 0,
            OpenFindings = open.Count,
            VerifiedFindings = findings.Count(f => f.Status == FindingStatus.Verified),
            AwaitingApproval = awaitingApproval,
            LicenseBlocked = findings.Count(f => f.Status == FindingStatus.BlockedByLicense),
            BySeverity = Enum.GetValues<Severity>()
                .ToDictionary(s => s, s => open.Count(f => f.Severity == s)),
            ByDomain = byDomain,
            ByNistFunction = byFunction,
            Trend = assessments
                .Where(a => a.ScopedControlReferences == null && a.CompletedAt is not null)
                .OrderBy(a => a.CompletedAt)
                .Select(a => new ScorePoint(a.CompletedAt!.Value, a.PostureScore))
                .ToList()
        };
    }
}

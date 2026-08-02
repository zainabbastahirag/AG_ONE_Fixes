using System.Diagnostics;
using System.Text.Json;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Evaluation;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgOneSafe.Application.Assessments;

/// <summary>
/// Drives a baseline scan: read the catalog out of SQL, fan the audit actions out across the
/// executors, judge each result with the assertion engine, then fold the outcomes into findings
/// and a posture score. No control-specific code lives here - that is the whole point.
/// </summary>
public sealed class AssessmentService : IAssessmentService
{
    private readonly IAgOneSafeDbContext _db;
    private readonly IExecutorRegistry _executors;
    private readonly IAssertionEvaluator _evaluator;
    private readonly IAuditTrail _audit;
    private readonly ILogger<AssessmentService> _logger;

    public AssessmentService(
        IAgOneSafeDbContext db,
        IExecutorRegistry executors,
        IAssertionEvaluator evaluator,
        IAuditTrail audit,
        ILogger<AssessmentService> logger)
    {
        _db = db;
        _executors = executors;
        _evaluator = evaluator;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Assessment> RunAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.TenantConnections
                         .FirstOrDefaultAsync(t => t.Id == request.TenantConnectionId, cancellationToken)
                     ?? throw new InvalidOperationException($"Tenant {request.TenantConnectionId} is not onboarded.");

        var targetLevel = request.TargetLevel ?? tenant.TargetLevel;

        var assessment = new Assessment
        {
            TenantConnectionId = tenant.Id,
            Status = AssessmentStatus.Running,
            Trigger = request.Trigger,
            TargetLevel = targetLevel,
            TriggeredBy = request.TriggeredBy,
            StartedAt = DateTimeOffset.UtcNow,
            ScopedControlReferences = request.ControlReferences is { Count: > 0 }
                ? string.Join(",", request.ControlReferences)
                : null
        };

        _db.Assessments.Add(assessment);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.AssessmentStarted,
            TenantConnectionId = tenant.Id,
            AssessmentId = assessment.Id,
            Actor = request.TriggeredBy,
            ActorRole = request.Trigger == AssessmentTrigger.Manual ? "User" : "Agent",
            Summary = $"Baseline scan started against {tenant.DisplayName} at target {targetLevel}.",
            Details = new { request.Trigger, TargetLevel = targetLevel.ToString(), assessment.ScopedControlReferences }
        }, cancellationToken);

        try
        {
            var controls = await LoadControlsAsync(request, cancellationToken);
            var evaluations = await EvaluateAsync(tenant, controls, request.MaxConcurrency, cancellationToken);

            await PersistAsync(assessment, tenant, controls, evaluations, targetLevel, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Assessment {AssessmentId} failed", assessment.Id);
            assessment.Status = AssessmentStatus.Failed;
            assessment.FailureReason = ex.Message;
            assessment.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        return assessment;
    }

    private async Task<List<ControlDefinition>> LoadControlsAsync(
        AssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var query = _db.ControlDefinitions
            .AsNoTracking()
            .Include(c => c.Actions)
            .ThenInclude(a => a.Assertions)
            .Where(c => c.IsEnabled);

        if (request.Benchmark is { } benchmark)
        {
            query = query.Where(c => c.Benchmark == benchmark);
        }

        if (request.ControlReferences is { Count: > 0 })
        {
            var references = request.ControlReferences.ToList();
            query = query.Where(c => references.Contains(c.VendorReference));
        }

        return await query
            .OrderBy(c => c.Benchmark)
            .ThenBy(c => c.VendorReference)
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<int, ControlEvaluation>> EvaluateAsync(
        TenantConnection tenant,
        IReadOnlyList<ControlDefinition> controls,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<int, ControlEvaluation>();
        var gate = new SemaphoreSlim(Math.Max(1, maxConcurrency));
        var sync = new object();

        // Executors are stateless and never touch the DbContext, so the fan-out is safe here;
        // everything is written back on the calling thread once the wave completes.
        var work = controls.Select(async control =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var evaluation = await EvaluateControlAsync(tenant, control, cancellationToken);
                lock (sync)
                {
                    results[control.Id] = evaluation;
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(work);
        return results;
    }

    private async Task<ControlEvaluation> EvaluateControlAsync(
        TenantConnection tenant,
        ControlDefinition control,
        CancellationToken cancellationToken)
    {
        var action = control.AuditAction;
        if (action is null)
        {
            return new ControlEvaluation
            {
                Outcome = ControlOutcome.Manual,
                FailureReason = "No audit action is configured for this control.",
                RequiredAction = "Add an audit action to the control in the catalog."
            };
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var executor = _executors.Resolve(action.ExecutorType);
            var execution = await executor.ExecuteAsync(new ExecutionRequest
            {
                Tenant = tenant,
                Control = control,
                Action = action,
                DryRun = false,
                CancellationToken = cancellationToken
            });

            return _evaluator.Evaluate(control, action, execution);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Control {Reference} raised an executor error", control.VendorReference);
            return new ControlEvaluation
            {
                Outcome = ControlOutcome.Error,
                FailureReason = $"The audit action threw an unexpected error: {ex.Message}",
                RequiredAction = "Review the control payload and connector health, then re-run the scan.",
                ExecutorError = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task PersistAsync(
        Assessment assessment,
        TenantConnection tenant,
        IReadOnlyList<ControlDefinition> controls,
        IReadOnlyDictionary<int, ControlEvaluation> evaluations,
        ProfileLevel targetLevel,
        CancellationToken cancellationToken)
    {
        var existingFindings = await _db.Findings
            .Where(f => f.TenantConnectionId == tenant.Id)
            .ToListAsync(cancellationToken);

        double weightPassed = 0;
        double weightScored = 0;

        foreach (var control in controls)
        {
            var evaluation = evaluations[control.Id];
            var inScope = control.Level <= targetLevel;
            var outcome = inScope ? evaluation.Outcome : ControlOutcome.Skipped;

            var result = new ControlResult
            {
                AssessmentId = assessment.Id,
                ControlDefinitionId = control.Id,
                Outcome = outcome,
                FailureReason = inScope
                    ? evaluation.FailureReason
                    : $"Out of scope: this is a Level {(int)control.Level} control and the tenant target is Level {(int)targetLevel}.",
                RequiredAction = inScope ? evaluation.RequiredAction : null,
                EvidenceJson = evaluation.EvidenceJson,
                AssertionResultsJson = JsonSerializer.Serialize(evaluation.Assertions),
                ExecutedCommand = evaluation.ExecutedCommand,
                ExecutorError = evaluation.ExecutorError,
                DurationMs = evaluation.DurationMs
            };

            _db.ControlResults.Add(result);

            switch (outcome)
            {
                case ControlOutcome.Pass:
                    assessment.PassedCount++;
                    weightPassed += IAssessmentService.ControlWeight(control.Severity, control.RiskWeight);
                    weightScored += IAssessmentService.ControlWeight(control.Severity, control.RiskWeight);
                    break;
                case ControlOutcome.Fail:
                    assessment.FailedCount++;
                    weightScored += IAssessmentService.ControlWeight(control.Severity, control.RiskWeight);
                    break;
                case ControlOutcome.Error:
                    assessment.ErrorCount++;
                    break;
                case ControlOutcome.Manual:
                    assessment.ManualCount++;
                    break;
                default:
                    assessment.NotApplicableCount++;
                    break;
            }

            UpsertFinding(assessment, tenant, control, evaluation, outcome, existingFindings, result);
        }

        assessment.TotalControls = controls.Count;
        assessment.PostureScore = weightScored <= 0 ? 100 : Math.Round(weightPassed * 100.0 / weightScored, 1);
        assessment.Status = AssessmentStatus.Completed;
        assessment.CompletedAt = DateTimeOffset.UtcNow;

        tenant.LastAssessmentAt = assessment.CompletedAt;
        tenant.LatestPostureScore = assessment.PostureScore;

        await _db.SaveChangesAsync(cancellationToken);

        // Findings are linked to the control result only after both rows have identities.
        await LinkLatestResultsAsync(assessment.Id, cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.AssessmentCompleted,
            TenantConnectionId = tenant.Id,
            AssessmentId = assessment.Id,
            Actor = assessment.TriggeredBy,
            ActorRole = "Agent",
            Summary =
                $"Scan complete: {assessment.PassedCount} passed, {assessment.FailedCount} failed, posture {assessment.PostureScore}%.",
            Details = new
            {
                assessment.TotalControls,
                assessment.PassedCount,
                assessment.FailedCount,
                assessment.ErrorCount,
                assessment.ManualCount,
                assessment.PostureScore,
                DurationSeconds = (int)(assessment.Duration?.TotalSeconds ?? 0)
            }
        }, cancellationToken);
    }

    private void UpsertFinding(
        Assessment assessment,
        TenantConnection tenant,
        ControlDefinition control,
        ControlEvaluation evaluation,
        ControlOutcome outcome,
        List<Finding> existingFindings,
        ControlResult result)
    {
        var finding = existingFindings.FirstOrDefault(f => f.ControlDefinitionId == control.Id);

        if (outcome == ControlOutcome.Fail || (outcome == ControlOutcome.Manual && !control.IsAutomatedAudit))
        {
            var status = control.RequiresLicenseUpgrade
                ? FindingStatus.BlockedByLicense
                : outcome == ControlOutcome.Manual
                    ? FindingStatus.ManualActionRequired
                    : FindingStatus.Open;

            if (finding is null)
            {
                finding = new Finding
                {
                    TenantConnectionId = tenant.Id,
                    ControlDefinitionId = control.Id,
                    FirstDetectedAssessmentId = assessment.Id,
                    Status = status,
                    Severity = control.Severity,
                    FirstDetectedAt = DateTimeOffset.UtcNow
                };
                _db.Findings.Add(finding);
                existingFindings.Add(finding);
            }
            else if (finding.Status is FindingStatus.Verified or FindingStatus.Remediated)
            {
                // The gap came back after a previously successful fix - treat it as a regression.
                finding.Status = status;
                finding.RemediatedAt = null;
                finding.VerifiedAt = null;
            }
            else if (finding.Status == FindingStatus.Open && status != FindingStatus.Open)
            {
                finding.Status = status;
            }

            finding.Severity = control.Severity;
            finding.FailureReason = Truncate(evaluation.FailureReason ?? "Control failed.", 4000);
            finding.RequiredAction = Truncate(evaluation.RequiredAction ?? string.Empty, 4000);
            finding.RiskScore = CalculateRiskScore(control);
            finding.LastSeenAt = DateTimeOffset.UtcNow;
            finding.LatestControlResult = result;
        }
        else if (outcome == ControlOutcome.Pass && finding is not null && finding.Status != FindingStatus.RiskAccepted)
        {
            // The control now passes: close the loop for anything that was remediated,
            // and drop findings that were fixed outside the platform.
            finding.Status = FindingStatus.Verified;
            finding.VerifiedAt = DateTimeOffset.UtcNow;
            finding.RemediatedAt ??= DateTimeOffset.UtcNow;
            finding.LastSeenAt = DateTimeOffset.UtcNow;
            finding.FailureReason = string.Empty;
            finding.RequiredAction = string.Empty;
            finding.LatestControlResult = result;
        }
    }

    private async Task LinkLatestResultsAsync(int assessmentId, CancellationToken cancellationToken)
    {
        var results = await _db.ControlResults
            .Where(r => r.AssessmentId == assessmentId)
            .Select(r => new { r.Id, r.ControlDefinitionId })
            .ToListAsync(cancellationToken);

        var map = results.ToDictionary(r => r.ControlDefinitionId, r => r.Id);

        var findings = await _db.Findings
            .Where(f => map.Keys.Contains(f.ControlDefinitionId))
            .ToListAsync(cancellationToken);

        foreach (var finding in findings.Where(finding => map.ContainsKey(finding.ControlDefinitionId)))
        {
            finding.LatestControlResultId = map[finding.ControlDefinitionId];
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>0-100 risk score blending severity, benchmark level and the control's risk weight.</summary>
    public static double CalculateRiskScore(ControlDefinition control)
    {
        var severity = (int)control.Severity / 4.0;         // 0.25 - 1.00
        var risk = Math.Clamp(control.RiskWeight, 1, 10) / 10.0;
        var level = control.Level == ProfileLevel.L1 ? 1.0 : 0.8;

        return Math.Round(severity * 55 + risk * 35 + level * 10, 1);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

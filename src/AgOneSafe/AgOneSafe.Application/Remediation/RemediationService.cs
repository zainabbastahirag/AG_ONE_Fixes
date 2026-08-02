using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Agent;
using AgOneSafe.Application.Assessments;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Catalog;
using AgOneSafe.Domain.Remediation;
using AgOneSafe.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgOneSafe.Application.Remediation;

/// <summary>
/// Runs the safety-gated remediation loop:
/// plan -> blast radius -> dry run -> approval -> snapshot -> execute -> verify -> (rollback).
/// Every transition writes an audit row, so the change log is a by-product of doing the work.
/// </summary>
public sealed class RemediationService : IRemediationService
{
    private readonly IAgOneSafeDbContext _db;
    private readonly IRemediationAgent _agent;
    private readonly IExecutorRegistry _executors;
    private readonly IPayloadRenderer _renderer;
    private readonly IAssessmentService _assessments;
    private readonly IAuditTrail _audit;
    private readonly ILogger<RemediationService> _logger;

    public RemediationService(
        IAgOneSafeDbContext db,
        IRemediationAgent agent,
        IExecutorRegistry executors,
        IPayloadRenderer renderer,
        IAssessmentService assessments,
        IAuditTrail audit,
        ILogger<RemediationService> logger)
    {
        _db = db;
        _agent = agent;
        _executors = executors;
        _renderer = renderer;
        _assessments = assessments;
        _audit = audit;
        _logger = logger;
    }

    public async Task<RemediationTask> PlanAsync(
        int findingId,
        Actor actor,
        int? batchId = null,
        CancellationToken cancellationToken = default)
    {
        var finding = await LoadFindingAsync(findingId, cancellationToken);
        var control = finding.ControlDefinition!;

        var existing = await _db.RemediationTasks
            .Include(t => t.ApprovalRequests)
            .ThenInclude(r => r.Signatures)
            .Include(t => t.Executions)
            .Where(t => t.FindingId == findingId &&
                        t.Status != RemediationStatus.Cancelled &&
                        t.Status != RemediationStatus.Rejected &&
                        t.Status != RemediationStatus.Succeeded &&
                        t.Status != RemediationStatus.RolledBack)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var plan = await _agent.PlanAsync(finding, control, cancellationToken);

        var task = existing ?? new RemediationTask
        {
            FindingId = finding.Id,
            TenantConnectionId = finding.TenantConnectionId,
            ControlDefinitionId = control.Id,
            CreatedBy = actor.Name,
            CreatedAt = DateTimeOffset.UtcNow
        };

        task.RemediationBatchId = batchId ?? task.RemediationBatchId;
        task.PlanSummary = plan.CanAct ? plan.Summary : plan.Advisory;
        task.ScriptPreview = plan.ScriptPreview;
        task.BlastRadiusJson = JsonSerializer.Serialize(plan.BlastRadius);
        task.ImpactSeverity = plan.ImpactSeverity;
        task.RequiresApproval = plan.RequiresApproval;
        task.RequiredSignatures = plan.RequiredSignatures;
        task.Status = RemediationStatus.Draft;

        if (existing is null)
        {
            _db.RemediationTasks.Add(task);
        }

        if (!plan.CanAct)
        {
            finding.Status = control.RequiresLicenseUpgrade
                ? FindingStatus.BlockedByLicense
                : FindingStatus.ManualActionRequired;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.RemediationPlanned,
            TenantConnectionId = finding.TenantConnectionId,
            ControlReference = control.VendorReference,
            FindingId = finding.Id,
            RemediationTaskId = task.Id,
            Actor = actor.Name,
            ActorRole = actor.Role,
            Summary = $"Plan generated for {control.VendorReference}: {task.PlanSummary}",
            Details = new
            {
                plan.CanAct,
                plan.RequiresApproval,
                plan.RequiredSignatures,
                ImpactSeverity = plan.ImpactSeverity.ToString(),
                plan.BlastRadius.TotalImpacted,
                Steps = plan.Steps.Select(s => s.Description)
            }
        }, cancellationToken);

        return task;
    }

    public async Task<RemediationExecution> DryRunAsync(int taskId, Actor actor, CancellationToken cancellationToken = default)
    {
        var (task, tenant, control) = await LoadTaskAsync(taskId, cancellationToken);
        var action = control.RemediationAction
                     ?? throw new InvalidOperationException($"Control {control.VendorReference} has no remediation action.");

        var execution = await RunAsync(task, tenant, control, action, RemediationMode.DryRun, actor, false, cancellationToken);

        if (task.Status == RemediationStatus.Draft)
        {
            task.Status = RemediationStatus.DryRunCompleted;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.RemediationDryRun,
            TenantConnectionId = tenant.Id,
            ControlReference = control.VendorReference,
            FindingId = task.FindingId,
            RemediationTaskId = task.Id,
            Actor = actor.Name,
            ActorRole = actor.Role,
            Summary = $"Dry run for {control.VendorReference} {(execution.Succeeded ? "succeeded" : "failed")}.",
            Details = new { execution.Succeeded, execution.DurationMs, execution.Error }
        }, cancellationToken);

        return execution;
    }

    public async Task<ApprovalRequest> RequestApprovalAsync(
        int taskId,
        Actor actor,
        string justification,
        CancellationToken cancellationToken = default)
    {
        var (task, tenant, control) = await LoadTaskAsync(taskId, cancellationToken);

        var request = new ApprovalRequest
        {
            RemediationTaskId = task.Id,
            RequiredSignatures = task.RequiredSignatures,
            Justification = justification,
            RequestedBy = actor.Name,
            RequestedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        _db.ApprovalRequests.Add(request);
        task.Status = RemediationStatus.PendingApproval;

        var finding = await _db.Findings.FirstAsync(f => f.Id == task.FindingId, cancellationToken);
        finding.Status = FindingStatus.Open;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.ApprovalRequested,
            TenantConnectionId = tenant.Id,
            ControlReference = control.VendorReference,
            FindingId = task.FindingId,
            RemediationTaskId = task.Id,
            Actor = actor.Name,
            ActorRole = actor.Role,
            Summary = $"Approval requested for {control.VendorReference} ({task.RequiredSignatures} signature(s) required).",
            Details = new { justification, task.RequiredSignatures, ImpactSeverity = task.ImpactSeverity.ToString() }
        }, cancellationToken);

        return request;
    }

    public async Task<ApprovalRequest> RecordDecisionAsync(
        int taskId,
        Actor approver,
        ApprovalDecision decision,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var (task, tenant, control) = await LoadTaskAsync(taskId, cancellationToken);

        var request = await _db.ApprovalRequests
                          .Include(r => r.Signatures)
                          .Where(r => r.RemediationTaskId == taskId && r.Decision == ApprovalDecision.Pending)
                          .OrderByDescending(r => r.Id)
                          .FirstOrDefaultAsync(cancellationToken)
                      ?? throw new InvalidOperationException("There is no approval pending for this task.");

        if (request.Signatures.Any(s => string.Equals(s.Approver, approver.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"{approver.Name} has already signed this request; multi-signature approval needs distinct approvers.");
        }

        request.Signatures.Add(new ApprovalSignature
        {
            Approver = approver.Name,
            ApproverRole = approver.Role,
            Decision = decision,
            Comment = comment,
            DecidedAt = DateTimeOffset.UtcNow
        });

        if (decision == ApprovalDecision.Rejected)
        {
            request.Decision = ApprovalDecision.Rejected;
            request.DecidedAt = DateTimeOffset.UtcNow;
            task.Status = RemediationStatus.Rejected;
        }
        else if (request.Signatures.Count(s => s.Decision == ApprovalDecision.Approved) >= request.RequiredSignatures)
        {
            request.Decision = ApprovalDecision.Approved;
            request.DecidedAt = DateTimeOffset.UtcNow;
            task.Status = task.ScheduledFor is null ? RemediationStatus.Approved : RemediationStatus.Scheduled;

            var finding = await _db.Findings.FirstAsync(f => f.Id == task.FindingId, cancellationToken);
            finding.Status = task.ScheduledFor is null ? FindingStatus.Approved : FindingStatus.Scheduled;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = decision == ApprovalDecision.Approved
                ? AuditEventType.ApprovalGranted
                : AuditEventType.ApprovalRejected,
            TenantConnectionId = tenant.Id,
            ControlReference = control.VendorReference,
            FindingId = task.FindingId,
            RemediationTaskId = task.Id,
            Actor = approver.Name,
            ActorRole = approver.Role,
            Summary =
                $"{approver.Name} ({approver.Role}) {decision.ToString().ToLowerInvariant()} remediation of {control.VendorReference}. " +
                $"{request.Signatures.Count(s => s.Decision == ApprovalDecision.Approved)}/{request.RequiredSignatures} signature(s).",
            Details = new { decision = decision.ToString(), comment, request.RequiredSignatures }
        }, cancellationToken);

        return request;
    }

    public async Task<RemediationTask> ExecuteAsync(int taskId, Actor actor, CancellationToken cancellationToken = default)
    {
        var (task, tenant, control) = await LoadTaskAsync(taskId, cancellationToken);

        var action = control.RemediationAction
                     ?? throw new InvalidOperationException($"Control {control.VendorReference} has no remediation action.");

        if (task.RequiresApproval && task.Status is not (RemediationStatus.Approved or RemediationStatus.Scheduled))
        {
            throw new InvalidOperationException(
                $"Remediation of {control.VendorReference} is at status {task.Status}; a human approval is still outstanding.");
        }

        if (control.RequiresLicenseUpgrade)
        {
            throw new InvalidOperationException(
                $"{control.VendorReference} needs {control.RequiredLicense}. Resolve the licence escalation first.");
        }

        task.Status = RemediationStatus.Running;
        await _db.SaveChangesAsync(cancellationToken);

        await CaptureSnapshotAsync(task, tenant, control, actor, cancellationToken);

        var execution = await RunAsync(task, tenant, control, action, RemediationMode.Execute, actor, false, cancellationToken);

        task.Status = execution.Succeeded ? RemediationStatus.Succeeded : RemediationStatus.Failed;
        task.CompletedAt = DateTimeOffset.UtcNow;

        var finding = await _db.Findings.FirstAsync(f => f.Id == task.FindingId, cancellationToken);
        finding.Status = execution.Succeeded ? FindingStatus.Remediated : FindingStatus.Open;
        finding.RemediatedAt = execution.Succeeded ? DateTimeOffset.UtcNow : null;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.RemediationExecuted,
            TenantConnectionId = tenant.Id,
            ControlReference = control.VendorReference,
            FindingId = task.FindingId,
            RemediationTaskId = task.Id,
            Actor = actor.Name,
            ActorRole = actor.Role,
            Summary = execution.Succeeded
                ? $"Remediation of {control.VendorReference} executed in tenant {tenant.PrimaryDomain}."
                : $"Remediation of {control.VendorReference} failed: {execution.Error}",
            Details = new
            {
                execution.Succeeded,
                execution.DurationMs,
                execution.ExecutedCommand,
                execution.Error,
                SnapshotId = task.ConfigurationSnapshotId
            }
        }, cancellationToken);

        if (execution.Succeeded)
        {
            await VerifyAsync(task, tenant, control, actor, cancellationToken);
        }

        return task;
    }

    public async Task<RemediationTask> RollbackAsync(int taskId, Actor actor, CancellationToken cancellationToken = default)
    {
        var (task, tenant, control) = await LoadTaskAsync(taskId, cancellationToken);

        if (task.ConfigurationSnapshotId is null)
        {
            throw new InvalidOperationException("No pre-execution snapshot exists, so this change cannot be rolled back.");
        }

        var snapshot = await _db.ConfigurationSnapshots
            .FirstAsync(s => s.Id == task.ConfigurationSnapshotId, cancellationToken);

        if (!VerifySnapshotIntegrity(snapshot))
        {
            throw new InvalidOperationException(
                "The snapshot's integrity hash does not match its contents; refusing to restore a tampered state.");
        }

        var rollbackAction = control.RollbackAction ?? new ControlAction
        {
            Kind = ControlActionKind.Rollback,
            Name = $"Restore {control.VendorReference}",
            ExecutorType = snapshot.ExecutorType,
            Module = snapshot.Module,
            Payload = snapshot.RollbackPayload,
            ParametersJson = snapshot.StateJson,
            TimeoutSeconds = 180,
            SupportsDryRun = false
        };

        // The snapshot's captured values become the parameters, so the payload writes back
        // exactly what was there before the fix.
        var overrides = ReadSnapshotValues(snapshot);

        var execution = await RunAsync(task, tenant, control, rollbackAction, RemediationMode.Execute, actor, true, cancellationToken, overrides);

        if (execution.Succeeded)
        {
            task.Status = RemediationStatus.RolledBack;
            task.RolledBackAt = DateTimeOffset.UtcNow;
            task.RolledBackBy = actor.Name;

            var finding = await _db.Findings.FirstAsync(f => f.Id == task.FindingId, cancellationToken);
            finding.Status = FindingStatus.Open;
            finding.RemediatedAt = null;
            finding.VerifiedAt = null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.RollbackExecuted,
            TenantConnectionId = tenant.Id,
            ControlReference = control.VendorReference,
            FindingId = task.FindingId,
            RemediationTaskId = task.Id,
            Actor = actor.Name,
            ActorRole = actor.Role,
            Summary = execution.Succeeded
                ? $"Rolled {control.VendorReference} back to the configuration captured at {snapshot.CapturedAt:u}."
                : $"Rollback of {control.VendorReference} failed: {execution.Error}",
            Details = new { snapshot.StateHash, snapshot.CapturedAt, execution.Succeeded, execution.Error }
        }, cancellationToken);

        if (execution.Succeeded)
        {
            await VerifyAsync(task, tenant, control, actor, cancellationToken);
        }

        return task;
    }

    public async Task<RemediationBatch> CreateBatchAsync(
        int tenantConnectionId,
        IReadOnlyCollection<int> findingIds,
        string name,
        DateTimeOffset? scheduledFor,
        Actor actor,
        CancellationToken cancellationToken = default)
    {
        var batch = new RemediationBatch
        {
            TenantConnectionId = tenantConnectionId,
            Name = string.IsNullOrWhiteSpace(name) ? $"Wave {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}" : name,
            ScheduledFor = scheduledFor,
            CreatedBy = actor.Name,
            Status = RemediationStatus.Draft
        };

        _db.RemediationBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var findingId in findingIds)
        {
            var task = await PlanAsync(findingId, actor, batch.Id, cancellationToken);
            task.ScheduledFor = scheduledFor;
            if (scheduledFor is not null)
            {
                task.Status = RemediationStatus.Scheduled;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task<int> ExecuteBatchAsync(int batchId, Actor actor, CancellationToken cancellationToken = default)
    {
        var tasks = await _db.RemediationTasks
            .Where(t => t.RemediationBatchId == batchId &&
                        (t.Status == RemediationStatus.Approved ||
                         t.Status == RemediationStatus.Scheduled ||
                         (!t.RequiresApproval && t.Status != RemediationStatus.Succeeded)))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var executed = 0;
        foreach (var taskId in tasks)
        {
            try
            {
                await ExecuteAsync(taskId, actor, cancellationToken);
                executed++;
            }
            catch (Exception ex)
            {
                // One bad control must not abort the wave; the failure is on the task's own record.
                _logger.LogError(ex, "Batch {BatchId} task {TaskId} failed", batchId, taskId);
            }
        }

        var batch = await _db.RemediationBatches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
        if (batch is not null)
        {
            batch.Status = RemediationStatus.Succeeded;
            batch.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return executed;
    }

    public async Task<int> ExecuteDueScheduledAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var due = await _db.RemediationTasks
            .Where(t => t.Status == RemediationStatus.Scheduled &&
                        t.ScheduledFor != null &&
                        t.ScheduledFor <= now)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var executed = 0;
        foreach (var taskId in due)
        {
            try
            {
                await ExecuteAsync(taskId, Actor.Agent, cancellationToken);
                executed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled remediation {TaskId} failed", taskId);
            }
        }

        return executed;
    }

    public async Task AcceptRiskAsync(int findingId, Actor actor, string reason, CancellationToken cancellationToken = default)
    {
        var finding = await LoadFindingAsync(findingId, cancellationToken);

        finding.Status = FindingStatus.RiskAccepted;
        finding.RiskAcceptanceReason = reason;
        finding.RiskAcceptedBy = actor.Name;

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.RiskAccepted,
            TenantConnectionId = finding.TenantConnectionId,
            ControlReference = finding.ControlDefinition!.VendorReference,
            FindingId = finding.Id,
            Actor = actor.Name,
            ActorRole = actor.Role,
            Summary = $"{actor.Name} accepted the residual risk on {finding.ControlDefinition.VendorReference}.",
            Details = new { reason }
        }, cancellationToken);
    }

    private async Task<RemediationExecution> RunAsync(
        RemediationTask task,
        TenantConnection tenant,
        ControlDefinition control,
        ControlAction action,
        RemediationMode mode,
        Actor actor,
        bool isRollback,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? overrides = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var executor = _executors.Resolve(action.ExecutorType);

        ExecutionResult result;
        try
        {
            result = await executor.ExecuteAsync(new ExecutionRequest
            {
                Tenant = tenant,
                Control = control,
                Action = action,
                DryRun = mode == RemediationMode.DryRun,
                Parameters = overrides ?? new Dictionary<string, string>(),
                CancellationToken = cancellationToken
            });
        }
        catch (Exception ex)
        {
            result = ExecutionResult.Failure(ex.Message, durationMs: (int)stopwatch.ElapsedMilliseconds);
        }

        var execution = new RemediationExecution
        {
            RemediationTaskId = task.Id,
            Mode = mode,
            ExecutorType = action.ExecutorType,
            Succeeded = result.Succeeded,
            IsRollback = isRollback,
            ExecutedCommand = result.ExecutedCommand,
            Output = result.Output,
            Error = result.Error,
            DurationMs = result.DurationMs,
            ExecutedBy = actor.Name,
            StartedAt = DateTimeOffset.UtcNow.AddMilliseconds(-result.DurationMs),
            CompletedAt = DateTimeOffset.UtcNow
        };

        _db.RemediationExecutions.Add(execution);
        await _db.SaveChangesAsync(cancellationToken);

        return execution;
    }

    private async Task CaptureSnapshotAsync(
        RemediationTask task,
        TenantConnection tenant,
        ControlDefinition control,
        Actor actor,
        CancellationToken cancellationToken)
    {
        // Prefer a purpose-built snapshot action; otherwise re-read the audit action, which by
        // definition returns the settings the fix is about to change.
        var snapshotAction = control.Actions.FirstOrDefault(a => a.Kind == ControlActionKind.Snapshot)
                             ?? control.AuditAction;

        var stateJson = "{}";

        if (snapshotAction is not null)
        {
            var executor = _executors.Resolve(snapshotAction.ExecutorType);
            var result = await executor.ExecuteAsync(new ExecutionRequest
            {
                Tenant = tenant,
                Control = control,
                Action = snapshotAction,
                DryRun = false,
                CancellationToken = cancellationToken
            });

            if (result.Succeeded)
            {
                stateJson = result.Json;
            }
        }

        var rollback = control.RollbackAction;

        var snapshot = new ConfigurationSnapshot
        {
            TenantConnectionId = tenant.Id,
            ControlDefinitionId = control.Id,
            ControlReference = control.VendorReference,
            StateJson = stateJson,
            RollbackPayload = rollback?.Payload ?? string.Empty,
            ExecutorType = rollback?.ExecutorType ?? snapshotAction?.ExecutorType ?? ExecutorType.PowerShell,
            Module = rollback?.Module ?? snapshotAction?.Module ?? ConnectorModule.MicrosoftGraph,
            StateHash = Sha256(stateJson),
            CapturedBy = actor.Name
        };

        _db.ConfigurationSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);

        task.ConfigurationSnapshotId = snapshot.Id;
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.SnapshotCaptured,
            TenantConnectionId = tenant.Id,
            ControlReference = control.VendorReference,
            FindingId = task.FindingId,
            RemediationTaskId = task.Id,
            Actor = actor.Name,
            ActorRole = actor.Role,
            Summary = $"Pre-execution snapshot captured for {control.VendorReference} (hash {snapshot.StateHash[..12]}).",
            Details = new { snapshot.StateHash, snapshot.ExecutorType, snapshot.Module }
        }, cancellationToken);
    }

    private async Task VerifyAsync(
        RemediationTask task,
        TenantConnection tenant,
        ControlDefinition control,
        Actor actor,
        CancellationToken cancellationToken)
    {
        var verification = await _assessments.RunAsync(new AssessmentRequest
        {
            TenantConnectionId = tenant.Id,
            TargetLevel = tenant.TargetLevel,
            Trigger = AssessmentTrigger.PostRemediationVerification,
            TriggeredBy = "ag-one-agent",
            ControlReferences = new[] { control.VendorReference },
            MaxConcurrency = 1
        }, cancellationToken);

        var result = await _db.ControlResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.AssessmentId == verification.Id &&
                                      r.ControlDefinitionId == control.Id, cancellationToken);

        task.VerificationAssessmentId = verification.Id;
        task.VerificationOutcome = result?.Outcome;
        task.VerificationMessage = result?.Outcome == ControlOutcome.Pass
            ? $"Verified: {control.VendorReference} now passes its audit action."
            : result?.FailureReason ?? "Verification did not return a result.";

        var finding = await _db.Findings.FirstAsync(f => f.Id == task.FindingId, cancellationToken);

        if (task.Status == RemediationStatus.RolledBack)
        {
            finding.Status = result?.Outcome == ControlOutcome.Pass ? FindingStatus.Verified : FindingStatus.Open;
        }
        else
        {
            finding.Status = result?.Outcome == ControlOutcome.Pass
                ? FindingStatus.Verified
                : FindingStatus.VerificationFailed;
            finding.VerifiedAt = result?.Outcome == ControlOutcome.Pass ? DateTimeOffset.UtcNow : null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.VerificationCompleted,
            TenantConnectionId = tenant.Id,
            ControlReference = control.VendorReference,
            FindingId = task.FindingId,
            RemediationTaskId = task.Id,
            AssessmentId = verification.Id,
            Actor = "ag-one-agent",
            ActorRole = "Agent",
            Summary = task.VerificationMessage!,
            Details = new { Outcome = result?.Outcome.ToString(), FindingStatus = finding.Status.ToString() }
        }, cancellationToken);
    }

    private async Task<Finding> LoadFindingAsync(int findingId, CancellationToken cancellationToken) =>
        await _db.Findings
            .Include(f => f.ControlDefinition)
            .ThenInclude(c => c!.Actions)
            .ThenInclude(a => a.Assertions)
            .FirstOrDefaultAsync(f => f.Id == findingId, cancellationToken)
        ?? throw new InvalidOperationException($"Finding {findingId} not found.");

    private async Task<(RemediationTask Task, TenantConnection Tenant, ControlDefinition Control)> LoadTaskAsync(
        int taskId,
        CancellationToken cancellationToken)
    {
        var task = await _db.RemediationTasks
                       .Include(t => t.Executions)
                       .Include(t => t.ApprovalRequests)
                       .ThenInclude(r => r.Signatures)
                       .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken)
                   ?? throw new InvalidOperationException($"Remediation task {taskId} not found.");

        var tenant = await _db.TenantConnections.FirstAsync(t => t.Id == task.TenantConnectionId, cancellationToken);

        var control = await _db.ControlDefinitions
            .Include(c => c.Actions)
            .ThenInclude(a => a.Assertions)
            .FirstAsync(c => c.Id == task.ControlDefinitionId, cancellationToken);

        return (task, tenant, control);
    }

    private Dictionary<string, string> ReadSnapshotValues(ConfigurationSnapshot snapshot)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(snapshot.StateJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    values[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                        JsonValueKind.True => "$true",
                        JsonValueKind.False => "$false",
                        _ => property.Value.GetRawText()
                    };
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Snapshot {SnapshotId} state could not be parsed", snapshot.Id);
        }

        return values;
    }

    private static bool VerifySnapshotIntegrity(ConfigurationSnapshot snapshot) =>
        string.Equals(snapshot.StateHash, Sha256(snapshot.StateJson), StringComparison.OrdinalIgnoreCase);

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

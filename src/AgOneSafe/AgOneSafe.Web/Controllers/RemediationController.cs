using System.Text.Json;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Models;
using AgOneSafe.Application.Remediation;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Domain.Remediation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class RemediationHubViewModel
{
    public IReadOnlyList<Finding> AutoRemediable { get; init; } = Array.Empty<Finding>();
    public IReadOnlyList<RemediationTask> Tasks { get; init; } = Array.Empty<RemediationTask>();
    public IReadOnlyList<RemediationBatch> Batches { get; init; } = Array.Empty<RemediationBatch>();
}

public class RemediationTaskViewModel
{
    public required RemediationTask Task { get; init; }
    public required BlastRadius BlastRadius { get; init; }
    public IReadOnlyList<RemediationExecution> Executions { get; init; } = Array.Empty<RemediationExecution>();
}

[Authorize]
public class RemediationController : AgControllerBase
{
    private readonly IRemediationService _remediation;

    public RemediationController(IAgOneSafeDbContext db, IRemediationService remediation) : base(db) =>
        _remediation = remediation;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null)
        {
            return RedirectToAction("Connect", "Tenants");
        }

        var model = new RemediationHubViewModel
        {
            AutoRemediable = await Db.Findings
                .AsNoTracking()
                .Include(f => f.ControlDefinition)
                .Where(f => f.TenantConnectionId == tenant.Id &&
                            f.Status != FindingStatus.Verified &&
                            f.Status != FindingStatus.RiskAccepted &&
                            f.ControlDefinition!.IsAutomatedRemediation &&
                            f.ControlDefinition.RequiredLicense == null)
                .OrderByDescending(f => f.RiskScore)
                .ToListAsync(cancellationToken),
            Tasks = await Db.RemediationTasks
                .AsNoTracking()
                .Include(t => t.ControlDefinition)
                .Include(t => t.Executions)
                .Where(t => t.TenantConnectionId == tenant.Id)
                .OrderByDescending(t => t.Id)
                .Take(40)
                .ToListAsync(cancellationToken),
            Batches = await Db.RemediationBatches
                .AsNoTracking()
                .Include(b => b.Tasks)
                .Where(b => b.TenantConnectionId == tenant.Id)
                .OrderByDescending(b => b.Id)
                .Take(10)
                .ToListAsync(cancellationToken)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);

        var task = await Db.RemediationTasks
            .AsNoTracking()
            .Include(t => t.ControlDefinition)
            .Include(t => t.Finding)
            .Include(t => t.Executions)
            .Include(t => t.ApprovalRequests)
            .ThenInclude(r => r.Signatures)
            .Include(t => t.ConfigurationSnapshot)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        return View(new RemediationTaskViewModel
        {
            Task = task,
            BlastRadius = Deserialize(task.BlastRadiusJson),
            Executions = task.Executions.OrderByDescending(e => e.Id).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> Plan(int findingId, CancellationToken cancellationToken)
    {
        var task = await _remediation.PlanAsync(findingId, CurrentActor, cancellationToken: cancellationToken);
        return RedirectToAction(nameof(Details), new { id = task.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> DryRun(int id, CancellationToken cancellationToken)
    {
        var execution = await _remediation.DryRunAsync(id, CurrentActor, cancellationToken);
        TempData["Toast"] = execution.Succeeded
            ? "Dry run completed. Nothing was written to the tenant."
            : $"Dry run failed: {execution.Error}";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> RequestApproval(int id, string justification, CancellationToken cancellationToken)
    {
        await _remediation.RequestApprovalAsync(id, CurrentActor, justification ?? string.Empty, cancellationToken);
        TempData["Toast"] = "Sent to the approval queue.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> Execute(int id, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _remediation.ExecuteAsync(id, CurrentActor, cancellationToken);
            TempData["Toast"] = task.VerificationOutcome == ControlOutcome.Pass
                ? $"Remediation executed and verified: {task.VerificationMessage}"
                : $"Remediation finished with status {task.Status}. {task.VerificationMessage}";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Toast"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> Rollback(int id, CancellationToken cancellationToken)
    {
        try
        {
            var task = await _remediation.RollbackAsync(id, CurrentActor, cancellationToken);
            TempData["Toast"] = task.Status == RemediationStatus.RolledBack
                ? "Tenant configuration restored from the pre-execution snapshot."
                : "Rollback did not complete; see the execution log below.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Toast"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> CreateBatch(
        int[] findingIds,
        string name,
        DateTimeOffset? scheduledFor,
        CancellationToken cancellationToken)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);

        if (tenant is null || findingIds.Length == 0)
        {
            TempData["Toast"] = "Select at least one finding to build a remediation wave.";
            return RedirectToAction(nameof(Index));
        }

        var batch = await _remediation.CreateBatchAsync(
            tenant.Id, findingIds, name, scheduledFor, CurrentActor, cancellationToken);

        TempData["Toast"] = scheduledFor is null
            ? $"Wave '{batch.Name}' planned with {findingIds.Length} task(s). Each still needs approval."
            : $"Wave '{batch.Name}' scheduled for {scheduledFor:g} with {findingIds.Length} task(s).";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> ExecuteBatch(int id, CancellationToken cancellationToken)
    {
        var executed = await _remediation.ExecuteBatchAsync(id, CurrentActor, cancellationToken);
        TempData["Toast"] = $"{executed} approved task(s) executed in this wave.";
        return RedirectToAction(nameof(Index));
    }

    private static BlastRadius Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BlastRadius>(json) ?? new BlastRadius();
        }
        catch (JsonException)
        {
            return new BlastRadius();
        }
    }
}

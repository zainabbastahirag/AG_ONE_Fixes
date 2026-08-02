using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgOneSafe.Application.Agent;

/// <summary>Safety rules the agent must obey before it may touch a tenant.</summary>
public sealed class AgentPolicyOptions
{
    /// <summary>Approval is always demanded the first time a tenant is remediated, whatever the severity.</summary>
    public bool RequireApprovalOnFirstRemediation { get; set; } = true;

    /// <summary>Findings at or above this severity always need a signature.</summary>
    public Severity ApprovalThreshold { get; set; } = Severity.Medium;

    /// <summary>Impact tiers that demand two distinct approvers.</summary>
    public Severity MultiSignatureThreshold { get; set; } = Severity.Critical;

    /// <summary>Refuse to plan when the fix would touch more objects than this without review.</summary>
    public int LargeBlastRadiusThreshold { get; set; } = 250;
}

/// <summary>
/// Deterministic, auditable agent. It reads the control's own remediation definition, works out
/// the blast radius, applies the safety policy and emits either an execution plan or an advisory.
/// An LLM reasoner can replace this class wholesale - the pipeline only depends on
/// <see cref="IRemediationAgent"/>.
/// </summary>
public sealed class PolicyDrivenRemediationAgent : IRemediationAgent
{
    private readonly IAgOneSafeDbContext _db;
    private readonly IBlastRadiusAnalyzer _blastRadius;
    private readonly IPayloadRenderer _renderer;
    private readonly AgentPolicyOptions _policy;

    public PolicyDrivenRemediationAgent(
        IAgOneSafeDbContext db,
        IBlastRadiusAnalyzer blastRadius,
        IPayloadRenderer renderer,
        IOptions<AgentPolicyOptions> policy)
    {
        _db = db;
        _blastRadius = blastRadius;
        _renderer = renderer;
        _policy = policy.Value;
    }

    public async Task<AgentPlan> PlanAsync(
        Finding finding,
        ControlDefinition control,
        CancellationToken cancellationToken = default)
    {
        var remediation = control.RemediationAction;

        var latestResult = finding.LatestControlResultId is null
            ? null
            : await _db.ControlResults
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == finding.LatestControlResultId, cancellationToken);

        if (remediation is null || !control.IsAutomatedRemediation)
        {
            return Advisory(control, finding,
                "No automated remediation payload is published for this control, so AG ONE Safe will not act on the tenant.");
        }

        if (control.RequiresLicenseUpgrade)
        {
            return Advisory(control, finding,
                $"This fix needs {control.RequiredLicense}, which the tenant does not hold. Raise a licence escalation before the agent can execute.");
        }

        var radius = _blastRadius.Analyze(control, remediation, latestResult);

        if (radius.TotalImpacted > _policy.LargeBlastRadiusThreshold)
        {
            radius.Warnings.Add(
                $"Blast radius of {radius.TotalImpacted} objects exceeds the autonomous threshold of {_policy.LargeBlastRadiusThreshold}; a second approver has been added.");
        }

        var firstRemediation = _policy.RequireApprovalOnFirstRemediation &&
                               !await _db.RemediationTasks.AnyAsync(
                                   t => t.TenantConnectionId == finding.TenantConnectionId &&
                                        t.Status == RemediationStatus.Succeeded, cancellationToken);

        var requiresApproval = firstRemediation ||
                               finding.Severity >= _policy.ApprovalThreshold ||
                               radius.ImpactSeverity >= _policy.ApprovalThreshold ||
                               remediation.IsDestructive;

        var signatures = radius.ImpactSeverity >= _policy.MultiSignatureThreshold ||
                         remediation.IsDestructive ||
                         radius.TotalImpacted > _policy.LargeBlastRadiusThreshold
            ? 2
            : 1;

        var script = _renderer.Render(remediation, remediation.Payload);

        var steps = new List<AgentPlanStep>
        {
            new()
            {
                Order = 1,
                Description = "Capture pre-change configuration snapshot",
                Detail = $"Read the current state of {control.VendorReference} and store it with a SHA-256 integrity hash so the change can be reverted."
            },
            new()
            {
                Order = 2,
                Description = $"Rehearse the fix via {remediation.ExecutorType} dry run",
                Detail = remediation.SupportsDryRun
                    ? "Run the payload in -WhatIf / read-only mode and diff the projected result."
                    : "No dry-run variant is published; the preview is a static render of the payload."
            },
            new()
            {
                Order = 3,
                Description = requiresApproval
                    ? $"Hold for {signatures} human approval{(signatures > 1 ? "s" : string.Empty)}"
                    : "Auto-approve (low impact, policy permits)",
                Detail = firstRemediation
                    ? "First remediation on this tenant always stops at the human-in-the-loop gate."
                    : $"Impact tier {radius.ImpactSeverity} against threshold {_policy.ApprovalThreshold}.",
                IsAutomated = !requiresApproval
            },
            new()
            {
                Order = 4,
                Description = $"Execute in tenant via {remediation.Module}",
                Detail = Shorten(script)
            },
            new()
            {
                Order = 5,
                Description = "Re-run the control to verify the gap is closed",
                Detail = $"Targeted re-scan of {control.VendorReference}; the finding only reaches Verified when the audit action passes."
            }
        };

        return new AgentPlan
        {
            ControlDefinitionId = control.Id,
            ControlReference = control.VendorReference,
            CanAct = true,
            Summary =
                $"Apply the CIS {control.VendorReference} baseline to {control.Domain} using {remediation.ExecutorType}/{remediation.Module}, affecting {radius.TotalImpacted} object(s).",
            Steps = steps,
            BlastRadius = radius,
            RequiresApproval = requiresApproval,
            RequiredSignatures = signatures,
            ImpactSeverity = radius.ImpactSeverity,
            ScriptPreview = script,
            ExecutorType = remediation.ExecutorType
        };
    }

    private AgentPlan Advisory(ControlDefinition control, Finding finding, string reason)
    {
        var remediation = control.RemediationAction;

        var steps = new List<AgentPlanStep>
        {
            new()
            {
                Order = 1,
                Description = "Review the gap",
                Detail = finding.FailureReason,
                IsAutomated = false
            },
            new()
            {
                Order = 2,
                Description = "Apply the documented remediation",
                Detail = string.IsNullOrWhiteSpace(remediation?.Payload)
                    ? finding.RequiredAction
                    : remediation!.Payload,
                IsAutomated = false
            },
            new()
            {
                Order = 3,
                Description = "Re-scan to confirm",
                Detail = $"Run a targeted assessment of {control.VendorReference} once the change is in place.",
                IsAutomated = false
            }
        };

        return new AgentPlan
        {
            ControlDefinitionId = control.Id,
            ControlReference = control.VendorReference,
            CanAct = false,
            Advisory = reason,
            Summary = $"Advisory only: {control.VendorReference} {control.Title}.",
            Steps = steps,
            BlastRadius = new Models.BlastRadius
            {
                ExpectedUserImpact = control.UserImpact,
                IsReversible = false,
                ImpactSeverity = control.Severity
            },
            RequiresApproval = true,
            RequiredSignatures = 1,
            ImpactSeverity = control.Severity,
            ScriptPreview = remediation?.Payload ?? finding.RequiredAction,
            ExecutorType = remediation?.ExecutorType ?? ExecutorType.Manual
        };
    }

    private static string Shorten(string script)
    {
        var trimmed = script.Trim();
        return trimmed.Length <= 400 ? trimmed : trimmed[..400] + " ...";
    }
}

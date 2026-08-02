using AgOneSafe.Application.Models;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Catalog;

namespace AgOneSafe.Application.Agent;

/// <summary>What the agent decided to do about a gap, and why.</summary>
public sealed class AgentPlan
{
    public required int ControlDefinitionId { get; init; }
    public required string ControlReference { get; init; }

    /// <summary>True when a payload exists and policy permits the agent to run it.</summary>
    public bool CanAct { get; init; }

    /// <summary>Advisory text used when the agent must hand the gap to a human.</summary>
    public string Advisory { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public List<AgentPlanStep> Steps { get; init; } = new();

    public required BlastRadius BlastRadius { get; init; }

    public bool RequiresApproval { get; init; } = true;

    public int RequiredSignatures { get; init; } = 1;

    public Severity ImpactSeverity { get; init; } = Severity.Low;

    /// <summary>The payload rendered exactly as it will be sent to the tenant.</summary>
    public string ScriptPreview { get; init; } = string.Empty;

    public ExecutorType ExecutorType { get; init; }
}

public sealed class AgentPlanStep
{
    public int Order { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;

    /// <summary>False for advisory steps a human must carry out.</summary>
    public bool IsAutomated { get; init; } = true;
}

/// <summary>
/// Decides whether AG ONE Safe acts on a finding or advises on it. The default implementation is
/// deterministic and policy-driven; swap in an LLM-backed reasoner behind this interface without
/// changing the execution pipeline or the safety gates.
/// </summary>
public interface IRemediationAgent
{
    Task<AgentPlan> PlanAsync(Finding finding, ControlDefinition control, CancellationToken cancellationToken = default);
}

using AgOneSafe.Domain;

namespace AgOneSafe.Application.Models;

/// <summary>What a fix will touch, computed before anyone is asked to approve it.</summary>
public sealed class BlastRadius
{
    /// <summary>Directory objects (users, groups, service principals) affected.</summary>
    public int ImpactedUsers { get; set; }

    /// <summary>Azure resources affected (NSGs, storage accounts, app services).</summary>
    public int ImpactedResources { get; set; }

    /// <summary>Policies, transport rules or tenant settings rewritten.</summary>
    public int ImpactedPolicies { get; set; }

    public Severity ImpactSeverity { get; set; } = Severity.Low;

    /// <summary>Named objects shown in the preview table, capped for readability.</summary>
    public List<ImpactedObject> Objects { get; set; } = new();

    /// <summary>Operational warnings, e.g. "users on legacy Outlook clients will be signed out".</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Whether the change can be undone from a snapshot.</summary>
    public bool IsReversible { get; set; } = true;

    public string ExpectedUserImpact { get; set; } = "No Impact";

    /// <summary>Estimated seconds the change takes in-tenant, for maintenance-window planning.</summary>
    public int EstimatedDurationSeconds { get; set; } = 30;

    public int TotalImpacted => ImpactedUsers + ImpactedResources + ImpactedPolicies;
}

public sealed class ImpactedObject
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string ProposedValue { get; set; } = string.Empty;
}

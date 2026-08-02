using System.Text.Json;
using AgOneSafe.Application.Evaluation;
using AgOneSafe.Application.Models;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Catalog;

namespace AgOneSafe.Application.Agent;

public interface IBlastRadiusAnalyzer
{
    BlastRadius Analyze(ControlDefinition control, ControlAction remediation, ControlResult? latestResult);
}

/// <summary>
/// Estimates what a fix will touch by replaying the audit evidence that proved the gap.
/// The remediation action declares how to read its own impact through these parameters:
/// <c>impactScope</c> (tenant|users|resources|policies), <c>impactPath</c> (JSON path into the
/// evidence listing affected objects), <c>impactNamePath</c>, <c>reversible</c> and
/// <c>estimatedSeconds</c>.
/// </summary>
public sealed class BlastRadiusAnalyzer : IBlastRadiusAnalyzer
{
    private const int MaxListedObjects = 25;

    public BlastRadius Analyze(ControlDefinition control, ControlAction remediation, ControlResult? latestResult)
    {
        var parameters = ParseParameters(remediation.ParametersJson);

        var scope = Get(parameters, "impactScope", "policies");
        var impactPath = Get(parameters, "impactPath", string.Empty);
        var namePath = Get(parameters, "impactNamePath", "displayName");
        var reversible = !string.Equals(Get(parameters, "reversible", "true"), "false", StringComparison.OrdinalIgnoreCase);
        var estimatedSeconds = int.TryParse(Get(parameters, "estimatedSeconds", "30"), out var seconds) ? seconds : 30;

        var radius = new BlastRadius
        {
            ExpectedUserImpact = control.UserImpact,
            IsReversible = reversible && remediation.Kind == ControlActionKind.Remediate,
            EstimatedDurationSeconds = estimatedSeconds,
            ImpactSeverity = DetermineImpactSeverity(control, remediation)
        };

        var objects = ExtractObjects(latestResult, impactPath, namePath, parameters);
        radius.Objects.AddRange(objects.Take(MaxListedObjects));

        var count = objects.Count;

        switch (scope.ToLowerInvariant())
        {
            case "users":
                radius.ImpactedUsers = count;
                break;
            case "resources":
                radius.ImpactedResources = count;
                break;
            case "tenant":
                radius.ImpactedPolicies = Math.Max(1, count);
                break;
            default:
                radius.ImpactedPolicies = Math.Max(1, count);
                break;
        }

        if (remediation.IsDestructive)
        {
            radius.Warnings.Add(
                "This change is classed as destructive: it can remove access or delete configuration. Two approvals are required.");
        }

        if (!radius.IsReversible)
        {
            radius.Warnings.Add("No rollback payload is defined for this control - the change cannot be undone automatically.");
        }

        if (!string.Equals(control.UserImpact, "No Impact", StringComparison.OrdinalIgnoreCase))
        {
            radius.Warnings.Add(control.UserImpact);
        }

        if (control.Domain.Contains("Identity", StringComparison.OrdinalIgnoreCase) && radius.ImpactedUsers > 0)
        {
            radius.Warnings.Add(
                $"{radius.ImpactedUsers} sign-in identities are in scope. Confirm a break-glass account is excluded before executing.");
        }

        return radius;
    }

    private static List<ImpactedObject> ExtractObjects(
        ControlResult? result,
        string impactPath,
        string namePath,
        IReadOnlyDictionary<string, string> parameters)
    {
        var objects = new List<ImpactedObject>();

        if (result is null || string.IsNullOrWhiteSpace(impactPath) || string.IsNullOrWhiteSpace(result.EvidenceJson))
        {
            return objects;
        }

        try
        {
            using var document = JsonDocument.Parse(result.EvidenceJson);
            var resolved = JsonValueResolver.Resolve(document.RootElement, impactPath);

            if (!resolved.Exists)
            {
                return objects;
            }

            var elements = resolved.Values.Count == 1 && resolved.Values[0].ValueKind == JsonValueKind.Array
                ? resolved.Values[0].EnumerateArray().ToList()
                : resolved.Values.ToList();

            var proposed = Get(parameters, "proposedValue", "compliant");
            var objectType = Get(parameters, "impactObjectType", "Configuration");

            foreach (var element in elements)
            {
                var name = element.ValueKind == JsonValueKind.Object
                    ? JsonValueResolver.Stringify(JsonValueResolver.Resolve(element, namePath))
                    : JsonValueResolver.Stringify(element);

                var current = element.ValueKind == JsonValueKind.Object
                    ? JsonValueResolver.Stringify(JsonValueResolver.Resolve(element, Get(parameters, "impactValuePath", "value")))
                    : JsonValueResolver.Stringify(element);

                objects.Add(new ImpactedObject
                {
                    Type = objectType,
                    Name = name == "not present" ? "(unnamed)" : name,
                    CurrentValue = current == "not present" ? "non-compliant" : current,
                    ProposedValue = proposed
                });
            }
        }
        catch (JsonException)
        {
            // Evidence that will not parse simply yields an empty preview rather than blocking the plan.
        }

        return objects;
    }

    private static Severity DetermineImpactSeverity(ControlDefinition control, ControlAction remediation)
    {
        if (remediation.IsDestructive)
        {
            return Severity.Critical;
        }

        var impactsUsers = !control.UserImpact.Equals("No Impact", StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrWhiteSpace(control.UserImpact);

        return (control.Severity, impactsUsers) switch
        {
            (Severity.Critical, true) => Severity.Critical,
            (Severity.Critical, false) => Severity.High,
            (_, true) => Severity.High,
            (Severity.High, false) => Severity.Medium,
            _ => Severity.Low
        };
    }

    private static Dictionary<string, string> ParseParameters(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonValueResolver.Stringify(p.Value), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string Get(IReadOnlyDictionary<string, string> parameters, string key, string fallback) =>
        parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
}

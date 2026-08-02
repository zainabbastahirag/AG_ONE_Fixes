using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Catalog;

namespace AgOneSafe.Application.Evaluation;

public interface IAssertionEvaluator
{
    ControlEvaluation Evaluate(ControlDefinition control, ControlAction action, ExecutionResult execution);
}

/// <summary>
/// Applies a control's stored assertions to executor output. This is the only place that decides
/// pass or fail, which keeps every control - PowerShell, Graph or ARM - judged the same way.
/// </summary>
public sealed class AssertionEvaluator : IAssertionEvaluator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    public ControlEvaluation Evaluate(ControlDefinition control, ControlAction action, ExecutionResult execution)
    {
        if (execution.RequiresManualAction)
        {
            return new ControlEvaluation
            {
                Outcome = ControlOutcome.Manual,
                FailureReason = "This control cannot be verified automatically and needs an analyst to confirm it in the admin portal.",
                RequiredAction = execution.Output,
                EvidenceJson = execution.Json,
                ExecutedCommand = execution.ExecutedCommand,
                DurationMs = execution.DurationMs
            };
        }

        if (!execution.Succeeded)
        {
            return new ControlEvaluation
            {
                Outcome = ControlOutcome.Error,
                FailureReason = $"The audit action could not be completed: {execution.Error}",
                RequiredAction = "Check connector health and the app registration's permission grants, then re-run the scan.",
                EvidenceJson = execution.Json,
                ExecutedCommand = execution.ExecutedCommand,
                ExecutorError = execution.Error,
                DurationMs = execution.DurationMs
            };
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(execution.Json) ? "{}" : execution.Json);
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return new ControlEvaluation
            {
                Outcome = ControlOutcome.Error,
                FailureReason = $"The audit action returned output that is not valid JSON: {ex.Message}",
                RequiredAction = "Fix the control's audit payload so it emits JSON (PowerShell: pipe through ConvertTo-Json).",
                EvidenceJson = "{}",
                ExecutedCommand = execution.ExecutedCommand,
                ExecutorError = ex.Message,
                DurationMs = execution.DurationMs
            };
        }

        var assertions = action.Assertions.OrderBy(a => a.Sequence).ToList();
        if (assertions.Count == 0)
        {
            return new ControlEvaluation
            {
                Outcome = ControlOutcome.Manual,
                FailureReason = "No assertions are configured for this control, so its result needs manual review.",
                RequiredAction = "Add at least one assertion rule to the control's audit action.",
                EvidenceJson = execution.Json,
                ExecutedCommand = execution.ExecutedCommand,
                DurationMs = execution.DurationMs
            };
        }

        var outcomes = assertions.Select(rule => EvaluateRule(rule, root)).ToList();

        var passed = action.AssertionLogic == AssertionLogic.All
            ? outcomes.All(o => o.Passed)
            : outcomes.Any(o => o.Passed);

        if (passed)
        {
            return new ControlEvaluation
            {
                Outcome = ControlOutcome.Pass,
                Assertions = outcomes,
                EvidenceJson = execution.Json,
                ExecutedCommand = execution.ExecutedCommand,
                DurationMs = execution.DurationMs
            };
        }

        var failed = outcomes.Where(o => !o.Passed).ToList();

        var reason = new StringBuilder();
        foreach (var outcome in failed)
        {
            reason.AppendLine(outcome.Message);
        }

        var actions = failed
            .Select(o => o.RemediationHint)
            .Where(hint => !string.IsNullOrWhiteSpace(hint))
            .Distinct()
            .ToList();

        if (actions.Count == 0 && !string.IsNullOrWhiteSpace(control.RemediationAction?.Name))
        {
            actions.Add(control.RemediationAction!.Name);
        }

        return new ControlEvaluation
        {
            Outcome = ControlOutcome.Fail,
            FailureReason = reason.ToString().TrimEnd(),
            RequiredAction = string.Join(Environment.NewLine, actions),
            Assertions = outcomes,
            EvidenceJson = execution.Json,
            ExecutedCommand = execution.ExecutedCommand,
            DurationMs = execution.DurationMs
        };
    }

    private static AssertionOutcome EvaluateRule(AssertionRule rule, JsonElement root)
    {
        var resolved = JsonValueResolver.Resolve(root, rule.JsonPath);
        var actual = JsonValueResolver.Stringify(resolved);
        var passed = Compare(rule, resolved);

        var message = passed
            ? null
            : Render(rule.FailureMessage, rule, actual, resolved.Values.Count);

        return new AssertionOutcome
        {
            Sequence = rule.Sequence,
            JsonPath = rule.JsonPath,
            Operator = rule.Operator,
            Expected = rule.ExpectedValue,
            Actual = actual,
            Passed = passed,
            Message = message,
            RemediationHint = passed ? null : rule.RemediationHint
        };
    }

    private static bool Compare(AssertionRule rule, ResolvedValue resolved)
    {
        switch (rule.Operator)
        {
            case AssertionOperator.Exists:
                return resolved.Exists;
            case AssertionOperator.NotExists:
                return !resolved.Exists;
        }

        if (!resolved.Exists)
        {
            // A missing path is a fail for every value comparison except the emptiness checks.
            return rule.Operator is AssertionOperator.IsEmpty;
        }

        var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var values = resolved.Values;
        var first = values[0];

        switch (rule.Operator)
        {
            case AssertionOperator.Equals:
                // A projection must match on every element; a scalar just has to match.
                return values.All(v => ValueEquals(v, rule.ExpectedValue, comparison));

            case AssertionOperator.AllEqual:
                return values.All(v => ValueEquals(v, rule.ExpectedValue, comparison));

            case AssertionOperator.AnyEqual:
                return values.Any(v => ValueEquals(v, rule.ExpectedValue, comparison));

            case AssertionOperator.NotEquals:
                return values.All(v => !ValueEquals(v, rule.ExpectedValue, comparison));

            case AssertionOperator.IsTrue:
                return values.All(IsTrue);

            case AssertionOperator.IsFalse:
                return values.All(v => !IsTrue(v));

            case AssertionOperator.Contains:
                return values.Any(v => ContainsValue(v, rule.ExpectedValue, comparison));

            case AssertionOperator.NotContains:
                return values.All(v => !ContainsValue(v, rule.ExpectedValue, comparison));

            case AssertionOperator.Regex:
                return values.All(v => SafeRegexMatch(JsonValueResolver.Stringify(v), rule.ExpectedValue, rule.CaseSensitive));

            case AssertionOperator.IsEmpty:
                return values.All(IsEmpty);

            case AssertionOperator.IsNotEmpty:
                return values.All(v => !IsEmpty(v));

            case AssertionOperator.GreaterThan:
            case AssertionOperator.GreaterThanOrEqual:
            case AssertionOperator.LessThan:
            case AssertionOperator.LessThanOrEqual:
            case AssertionOperator.Between:
                return CompareNumeric(rule, values);

            case AssertionOperator.CountEquals:
            case AssertionOperator.CountGreaterThanOrEqual:
            case AssertionOperator.CountLessThanOrEqual:
            case AssertionOperator.CountBetween:
                return CompareCount(rule, resolved, first);

            default:
                return false;
        }
    }

    private static bool CompareNumeric(AssertionRule rule, IReadOnlyList<JsonElement> values)
    {
        if (!TryParseDouble(rule.ExpectedValue, out var expected))
        {
            return false;
        }

        foreach (var value in values)
        {
            if (!TryToDouble(value, out var actual))
            {
                return false;
            }

            var ok = rule.Operator switch
            {
                AssertionOperator.GreaterThan => actual > expected,
                AssertionOperator.GreaterThanOrEqual => actual >= expected,
                AssertionOperator.LessThan => actual < expected,
                AssertionOperator.LessThanOrEqual => actual <= expected,
                AssertionOperator.Between => TryParseDouble(rule.SecondaryValue, out var upper)
                                             && actual >= expected && actual <= upper,
                _ => false
            };

            if (!ok)
            {
                return false;
            }
        }

        return values.Count > 0;
    }

    private static bool CompareCount(AssertionRule rule, ResolvedValue resolved, JsonElement first)
    {
        // A wildcard projection counts the projected values; otherwise count array members.
        var count = resolved.IsProjection
            ? resolved.Values.Count
            : first.ValueKind == JsonValueKind.Array
                ? first.GetArrayLength()
                : resolved.Values.Count;

        if (!TryParseDouble(rule.ExpectedValue, out var expected))
        {
            return false;
        }

        return rule.Operator switch
        {
            AssertionOperator.CountEquals => Math.Abs(count - expected) < double.Epsilon,
            AssertionOperator.CountGreaterThanOrEqual => count >= expected,
            AssertionOperator.CountLessThanOrEqual => count <= expected,
            AssertionOperator.CountBetween => TryParseDouble(rule.SecondaryValue, out var upper)
                                              && count >= expected && count <= upper,
            _ => false
        };
    }

    private static bool ValueEquals(JsonElement element, string? expected, StringComparison comparison)
    {
        expected ??= string.Empty;

        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return bool.TryParse(expected, out var expectedBool) &&
                   expectedBool == (element.ValueKind == JsonValueKind.True);
        }

        if (element.ValueKind == JsonValueKind.Number &&
            TryParseDouble(expected, out var expectedNumber) &&
            element.TryGetDouble(out var actualNumber))
        {
            return Math.Abs(actualNumber - expectedNumber) < 0.000001;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return expected.Equals("null", StringComparison.OrdinalIgnoreCase) || expected.Length == 0;
        }

        return string.Equals(JsonValueResolver.Stringify(element), expected, comparison);
    }

    private static bool ContainsValue(JsonElement element, string? expected, StringComparison comparison)
    {
        expected ??= string.Empty;

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Any(item => ValueEquals(item, expected, comparison));
        }

        return JsonValueResolver.Stringify(element).Contains(expected, comparison);
    }

    private static bool IsTrue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) && parsed,
        JsonValueKind.Number => element.TryGetDouble(out var number) && Math.Abs(number) > double.Epsilon,
        _ => false
    };

    private static bool IsEmpty(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => true,
        JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()),
        JsonValueKind.Array => element.GetArrayLength() == 0,
        JsonValueKind.Object => !element.EnumerateObject().Any(),
        _ => false
    };

    private static bool SafeRegexMatch(string input, string? pattern, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            return Regex.IsMatch(input, pattern, options, RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryToDouble(JsonElement element, out double value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when element.TryGetDouble(out value):
                return true;
            case JsonValueKind.String:
                return TryParseDouble(element.GetString(), out value);
            case JsonValueKind.Array:
                value = element.GetArrayLength();
                return true;
            case JsonValueKind.True:
                value = 1;
                return true;
            case JsonValueKind.False:
                value = 0;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static bool TryParseDouble(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static string Render(string template, AssertionRule rule, string actual, int count)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "{path} is {actual} but the benchmark requires {expected}.";
        }

        return template
            .Replace("{actual}", actual, StringComparison.OrdinalIgnoreCase)
            .Replace("{expected}", rule.ExpectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{secondary}", rule.SecondaryValue ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{path}", rule.JsonPath, StringComparison.OrdinalIgnoreCase)
            .Replace("{count}", count.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }
}

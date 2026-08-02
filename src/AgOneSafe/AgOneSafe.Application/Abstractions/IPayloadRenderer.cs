using System.Text.Json;
using AgOneSafe.Domain.Catalog;

namespace AgOneSafe.Application.Abstractions;

/// <summary>
/// Substitutes an action's stored parameters into its payload before execution or preview,
/// so the script an approver reads is byte-for-byte the script the agent runs.
/// </summary>
public interface IPayloadRenderer
{
    string Render(ControlAction action, string payload, IReadOnlyDictionary<string, string>? overrides = null);

    IReadOnlyDictionary<string, string> ReadParameters(ControlAction action);
}

/// <summary>
/// Tokens use the <c>{{name}}</c> form. Values are escaped for the target runtime so a catalog
/// entry cannot smuggle extra statements into a PowerShell payload.
/// </summary>
public sealed class PayloadRenderer : IPayloadRenderer
{
    public string Render(ControlAction action, string payload, IReadOnlyDictionary<string, string>? overrides = null)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return string.Empty;
        }

        var parameters = new Dictionary<string, string>(ReadParameters(action), StringComparer.OrdinalIgnoreCase);

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                parameters[pair.Key] = pair.Value;
            }
        }

        var rendered = payload;
        foreach (var pair in parameters)
        {
            rendered = rendered.Replace(
                "{{" + pair.Key + "}}",
                Escape(action.ExecutorType, pair.Value),
                StringComparison.OrdinalIgnoreCase);
        }

        return rendered;
    }

    public IReadOnlyDictionary<string, string> ReadParameters(ControlAction action)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(action.ParametersJson))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(action.ParametersJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => property.Value.GetRawText()
                };
            }
        }
        catch (JsonException)
        {
            // A malformed parameter blob leaves the payload untouched rather than failing the scan.
        }

        return result;
    }

    /// <summary>
    /// Catalog parameters are scalars - booleans, counts, domain names, SKU ids - so the safest
    /// rule for a shell payload is an allow-list. Anything that could chain a second statement
    /// (<c>;</c>, <c>|</c>, <c>&amp;</c>, a backtick, a newline, a sub-expression) is dropped rather
    /// than escaped, which keeps a tampered catalog row from becoming remote code execution.
    /// </summary>
    private static string Escape(Domain.ExecutorType executorType, string value)
    {
        if (executorType != Domain.ExecutorType.PowerShell)
        {
            return value;
        }

        return new string(value
            .Where(c => char.IsLetterOrDigit(c) ||
                        c is ' ' or '.' or ':' or '_' or '-' or '@' or '/' or '\\' or '$' or '*' or '+' or ',' or '#' or '\'' or '=')
            .ToArray());
    }
}

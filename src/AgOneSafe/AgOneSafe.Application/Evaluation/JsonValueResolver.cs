using System.Text.Json;

namespace AgOneSafe.Application.Evaluation;

/// <summary>Values a path pointed at, plus whether the path existed at all.</summary>
public sealed record ResolvedValue(bool Exists, bool IsProjection, IReadOnlyList<JsonElement> Values)
{
    public static readonly ResolvedValue Missing = new(false, false, Array.Empty<JsonElement>());

    public JsonElement? First => Values.Count > 0 ? Values[0] : null;
}

/// <summary>
/// Minimal, allocation-light path reader over executor output. Deliberately smaller than full
/// JSONPath: catalog authors only need property walks, array indexes and one wildcard projection.
/// <code>
///   ""                       -> the document root
///   "EnableSafeLinks"        -> a property
///   "value[0].state"         -> an array element then a property
///   "policies[*].AllowBox"   -> that property across every array element
/// </code>
/// </summary>
public static class JsonValueResolver
{
    public static ResolvedValue Resolve(JsonElement root, string? path)
    {
        var trimmed = (path ?? string.Empty).Trim();
        if (trimmed.StartsWith("$", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..].TrimStart('.');
        }

        if (string.IsNullOrEmpty(trimmed))
        {
            return new ResolvedValue(true, false, new[] { root });
        }

        var current = new List<JsonElement> { root };
        var projected = false;

        foreach (var segment in Tokenize(trimmed))
        {
            var next = new List<JsonElement>();

            foreach (var element in current)
            {
                switch (segment)
                {
                    case PropertySegment property:
                        if (element.ValueKind == JsonValueKind.Object &&
                            TryGetPropertyIgnoreCase(element, property.Name, out var value))
                        {
                            next.Add(value);
                        }
                        else if (element.ValueKind == JsonValueKind.Array)
                        {
                            // Tolerate single-object results that PowerShell rendered as a one-item array.
                            foreach (var item in element.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.Object &&
                                    TryGetPropertyIgnoreCase(item, property.Name, out var nested))
                                {
                                    next.Add(nested);
                                    projected = true;
                                }
                            }
                        }

                        break;

                    case IndexSegment index:
                        if (element.ValueKind == JsonValueKind.Array &&
                            index.Index < element.GetArrayLength())
                        {
                            next.Add(element[index.Index]);
                        }

                        break;

                    case WildcardSegment:
                        if (element.ValueKind == JsonValueKind.Array)
                        {
                            next.AddRange(element.EnumerateArray());
                            projected = true;
                        }
                        else
                        {
                            next.Add(element);
                        }

                        break;
                }
            }

            if (next.Count == 0)
            {
                return ResolvedValue.Missing;
            }

            current = next;
        }

        return new ResolvedValue(true, projected, current);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private abstract record Segment;

    private sealed record PropertySegment(string Name) : Segment;

    private sealed record IndexSegment(int Index) : Segment;

    private sealed record WildcardSegment : Segment;

    private static IEnumerable<Segment> Tokenize(string path)
    {
        var buffer = string.Empty;

        for (var i = 0; i < path.Length; i++)
        {
            var c = path[i];

            if (c == '.')
            {
                if (buffer.Length > 0)
                {
                    yield return new PropertySegment(buffer);
                    buffer = string.Empty;
                }

                continue;
            }

            if (c == '[')
            {
                if (buffer.Length > 0)
                {
                    yield return new PropertySegment(buffer);
                    buffer = string.Empty;
                }

                var close = path.IndexOf(']', i);
                if (close < 0)
                {
                    yield break;
                }

                var inner = path[(i + 1)..close].Trim();
                i = close;

                if (inner == "*")
                {
                    yield return new WildcardSegment();
                }
                else if (int.TryParse(inner, out var index))
                {
                    yield return new IndexSegment(index);
                }

                continue;
            }

            buffer += c;
        }

        if (buffer.Length > 0)
        {
            yield return new PropertySegment(buffer);
        }
    }

    /// <summary>Renders a value the way it should appear in a failure message.</summary>
    public static string Stringify(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        JsonValueKind.Array => "[" + string.Join(", ", element.EnumerateArray().Select(Stringify)) + "]",
        JsonValueKind.Object => element.GetRawText(),
        _ => element.GetRawText()
    };

    public static string Stringify(ResolvedValue resolved)
    {
        if (!resolved.Exists)
        {
            return "not present";
        }

        return resolved.Values.Count == 1
            ? Stringify(resolved.Values[0])
            : "[" + string.Join(", ", resolved.Values.Select(Stringify)) + "]";
    }
}

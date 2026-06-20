using TeamPulse.Models.Domain;

namespace TeamPulse.Models.ViewModels;

public static class UiHelpers
{
    public static (string css, string label) Badge(TeamStatus s) => s switch
    {
        TeamStatus.OnTrack => ("b-green", "On Track"),
        TeamStatus.AtRisk => ("b-amber", "At Risk"),
        TeamStatus.OnHold => ("b-gray", "On Hold"),
        TeamStatus.Blocked => ("b-red", "Blocked"),
        TeamStatus.Completed => ("b-blue", "Completed"),
        _ => ("b-gray", s.ToString())
    };

    public static (string css, string label) Badge(WorkItemStatus s) => s switch
    {
        WorkItemStatus.Backlog => ("b-gray", "Backlog"),
        WorkItemStatus.Todo => ("b-blue", "To Do"),
        WorkItemStatus.InProgress => ("b-amber", "In Progress"),
        WorkItemStatus.InReview => ("b-purple", "In Review"),
        WorkItemStatus.Blocked => ("b-red", "Blocked"),
        WorkItemStatus.Done => ("b-green", "Done"),
        _ => ("b-gray", s.ToString())
    };

    public static (string css, string label) Badge(WorkItemPriority p) => p switch
    {
        WorkItemPriority.Low => ("b-gray", "Low"),
        WorkItemPriority.Medium => ("b-blue", "Medium"),
        WorkItemPriority.High => ("b-amber", "High"),
        WorkItemPriority.Critical => ("b-red", "Critical"),
        _ => ("b-gray", p.ToString())
    };

    public static (string css, string label) Badge(ReleaseStatus s) => s switch
    {
        ReleaseStatus.Planned => ("b-gray", "Planned"),
        ReleaseStatus.InProgress => ("b-amber", "In Progress"),
        ReleaseStatus.Testing => ("b-purple", "Testing"),
        ReleaseStatus.Released => ("b-green", "Released"),
        ReleaseStatus.OnHold => ("b-blue", "On Hold"),
        ReleaseStatus.Cancelled => ("b-red", "Cancelled"),
        _ => ("b-gray", s.ToString())
    };

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0].Substring(0, 1).ToUpper()
            : (parts[0][0].ToString() + parts[^1][0]).ToUpper();
    }

    private static readonly string[] Palette =
    {
        "#4f46e5", "#0891b2", "#db2777", "#16a34a", "#ea580c",
        "#7c3aed", "#2563eb", "#e11d48", "#0d9488", "#65a30d"
    };

    public static string ColorFor(string? key)
    {
        if (string.IsNullOrEmpty(key)) return Palette[0];
        var hash = key.Aggregate(0, (acc, c) => acc + c);
        return Palette[Math.Abs(hash) % Palette.Length];
    }

    public static string Stars(double rating)
    {
        var full = (int)Math.Round(rating);
        return string.Concat(Enumerable.Repeat("★", full)) + string.Concat(Enumerable.Repeat("☆", 5 - full));
    }
}

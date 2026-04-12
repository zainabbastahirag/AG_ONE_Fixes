using UglyToad.PdfPig;
using VidCV.Web.Models;

namespace VidCV.Web.Services;

public class CvParserService
{
    public CvProfile ParsePdf(string filePath, string fileName)
    {
        var text = "";
        using (var document = PdfDocument.Open(filePath))
        {
            foreach (var page in document.GetPages())
            {
                text += page.Text + "\n";
            }
        }

        return ParseText(text, fileName);
    }

    public CvProfile ParseText(string rawText, string? fileName = null)
    {
        var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var profile = new CvProfile
        {
            CvFileName = fileName,
            FullName = ExtractName(lines),
            Email = ExtractPattern(rawText, @"[\w\.-]+@[\w\.-]+\.\w+"),
            Phone = ExtractPattern(rawText, @"[\+]?[\d\s\-\(\)]{7,15}"),
            LinkedInUrl = ExtractPattern(rawText, @"linkedin\.com/in/[\w\-]+"),
            JobTitle = ExtractSection(lines, new[] { "title", "position", "role", "designation" }),
            Summary = ExtractSection(lines, new[] { "summary", "objective", "profile", "about" }),
            Skills = ExtractSection(lines, new[] { "skills", "technical", "technologies", "competencies" }),
            Experience = ExtractSection(lines, new[] { "experience", "employment", "work history", "career" }),
            Education = ExtractSection(lines, new[] { "education", "qualification", "academic", "degree" }),
        };

        if (string.IsNullOrWhiteSpace(profile.FullName) && lines.Count > 0)
            profile.FullName = lines[0];

        if (!string.IsNullOrWhiteSpace(profile.LinkedInUrl) && !profile.LinkedInUrl.StartsWith("http"))
            profile.LinkedInUrl = "https://" + profile.LinkedInUrl;

        return profile;
    }

    private static string ExtractName(List<string> lines)
    {
        foreach (var line in lines.Take(5))
        {
            var clean = line.Trim();
            if (clean.Length >= 3 && clean.Length <= 60 &&
                !clean.Contains('@') && !clean.Contains("http") &&
                !clean.Any(char.IsDigit) &&
                clean.Split(' ').Length >= 2)
            {
                return clean;
            }
        }
        return lines.FirstOrDefault() ?? "Unknown";
    }

    private static string ExtractPattern(string text, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Value : "";
    }

    private static string ExtractSection(List<string> lines, string[] headers)
    {
        var collecting = false;
        var result = new List<string>();

        foreach (var line in lines)
        {
            var lower = line.ToLower().Trim();

            if (headers.Any(h => lower.Contains(h) && lower.Length < 80))
            {
                if (collecting && result.Count > 0) break;
                collecting = true;

                var afterHeader = line;
                foreach (var h in headers)
                {
                    var idx = lower.IndexOf(h);
                    if (idx >= 0)
                    {
                        afterHeader = line.Substring(idx + h.Length).TrimStart(':', '-', ' ');
                        break;
                    }
                }
                if (!string.IsNullOrWhiteSpace(afterHeader))
                    result.Add(afterHeader);
                continue;
            }

            if (collecting)
            {
                if (IsNewSectionHeader(lower)) break;
                result.Add(line);
            }
        }

        var text = string.Join(" ", result).Trim();
        return text.Length > 3000 ? text[..3000] : text;
    }

    private static bool IsNewSectionHeader(string lower)
    {
        var knownHeaders = new[]
        {
            "education", "experience", "skills", "summary", "objective",
            "references", "certifications", "projects", "languages",
            "interests", "awards", "publications", "volunteer"
        };
        return knownHeaders.Any(h => lower.StartsWith(h) && lower.Length < 40);
    }
}

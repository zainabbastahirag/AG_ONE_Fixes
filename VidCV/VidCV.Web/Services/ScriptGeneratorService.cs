using VidCV.Web.Models;

namespace VidCV.Web.Services;

public class ScriptGeneratorService
{
    private readonly AiScriptService _ai;

    public ScriptGeneratorService(AiScriptService ai)
    {
        _ai = ai;
    }

    public async Task<string> GenerateScriptAsync(CvProfile profile)
    {
        return await _ai.GenerateScriptAsync(profile);
    }

    public List<VideoSlide> GenerateSlides(CvProfile profile, string script, VideoTemplate template)
    {
        var paragraphs = script
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 10)
            .ToList();

        var slides = new List<VideoSlide>();

        // Slide 1 — Intro (name + title)
        slides.Add(new VideoSlide
        {
            Order = 1,
            Title = profile.FullName,
            Subtitle = profile.JobTitle ?? "Professional",
            Content = paragraphs.FirstOrDefault() ?? "",
            Duration = 5,
            Type = SlideType.Intro
        });

        // Slide 2 — About / Summary
        var aboutText = paragraphs.Count > 1 ? paragraphs[1] : profile.Summary ?? "";
        if (!string.IsNullOrWhiteSpace(aboutText))
        {
            slides.Add(new VideoSlide
            {
                Order = 2,
                Title = "About Me",
                Content = Truncate(aboutText, 220),
                Duration = 6,
                Type = SlideType.Content
            });
        }

        // Slide 3 — Experience
        var expText = paragraphs.Count > 2 ? paragraphs[2] : profile.Experience ?? "";
        if (!string.IsNullOrWhiteSpace(expText))
        {
            slides.Add(new VideoSlide
            {
                Order = 3,
                Title = "Experience",
                Content = Truncate(expText, 220),
                Duration = 6,
                Type = SlideType.Content
            });
        }

        // Slide 4 — Skills
        var skillsRaw = profile.Skills ?? "";
        if (!string.IsNullOrWhiteSpace(skillsRaw))
        {
            var topSkills = skillsRaw
                .Split(new[] { ',', ';', '|', '\n', '•', '·' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 1 && s.Length < 40)
                .Take(8)
                .ToList();

            if (topSkills.Count > 0)
            {
                slides.Add(new VideoSlide
                {
                    Order = 4,
                    Title = "Skills & Expertise",
                    Content = string.Join("  •  ", topSkills),
                    Duration = 5,
                    Type = SlideType.Skills
                });
            }
        }

        // Slide 5 — Education (if available)
        var eduText = paragraphs.Count > 3 ? paragraphs[3] : profile.Education ?? "";
        if (!string.IsNullOrWhiteSpace(eduText))
        {
            slides.Add(new VideoSlide
            {
                Order = 5,
                Title = "Education",
                Content = Truncate(eduText, 200),
                Duration = 5,
                Type = SlideType.Content
            });
        }

        // Slide 6 — Contact / CTA
        slides.Add(new VideoSlide
        {
            Order = slides.Count + 1,
            Title = "Let's Connect!",
            Subtitle = profile.Email,
            Content = profile.LinkedInUrl ?? "",
            Duration = 5,
            Type = SlideType.Contact
        });

        return slides;
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Replace("\n", " ").Replace("\r", " ").Trim();
        while (text.Contains("  ")) text = text.Replace("  ", " ");
        return text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";
    }
}

public class VideoSlide
{
    public int Order { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? Content { get; set; }
    public int Duration { get; set; } = 5;
    public SlideType Type { get; set; }
}

public enum SlideType
{
    Intro,
    Content,
    Skills,
    Contact
}

using VidCV.Web.Models;

namespace VidCV.Web.Services;

public class ScriptGeneratorService
{
    public string GenerateScript(CvProfile profile)
    {
        var sections = new List<string>();

        var greeting = !string.IsNullOrWhiteSpace(profile.JobTitle)
            ? $"Hi, I'm {profile.FullName}, a {profile.JobTitle}."
            : $"Hi, I'm {profile.FullName}.";
        sections.Add(greeting);

        if (!string.IsNullOrWhiteSpace(profile.Summary))
        {
            var summary = Truncate(profile.Summary, 200);
            sections.Add(summary);
        }

        if (!string.IsNullOrWhiteSpace(profile.Experience))
        {
            var exp = Truncate(profile.Experience, 250);
            sections.Add($"With experience in: {exp}");
        }

        if (!string.IsNullOrWhiteSpace(profile.Skills))
        {
            var topSkills = profile.Skills
                .Split(new[] { ',', ';', '|', '\n', '•', '·' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 1 && s.Length < 40)
                .Take(6)
                .ToList();

            if (topSkills.Count > 0)
                sections.Add($"My key skills include {string.Join(", ", topSkills)}.");
        }

        if (!string.IsNullOrWhiteSpace(profile.Education))
        {
            var edu = Truncate(profile.Education, 150);
            sections.Add($"Education: {edu}");
        }

        sections.Add("I'm passionate about delivering great results and always open to new opportunities. Let's connect!");

        return string.Join("\n\n", sections);
    }

    public List<VideoSlide> GenerateSlides(CvProfile profile, VideoTemplate template)
    {
        var slides = new List<VideoSlide>();

        slides.Add(new VideoSlide
        {
            Order = 1,
            Title = profile.FullName,
            Subtitle = profile.JobTitle ?? "Professional",
            Duration = 4,
            Type = SlideType.Intro
        });

        if (!string.IsNullOrWhiteSpace(profile.Summary))
        {
            slides.Add(new VideoSlide
            {
                Order = 2,
                Title = "About Me",
                Content = Truncate(profile.Summary, 180),
                Duration = 5,
                Type = SlideType.Content
            });
        }

        if (!string.IsNullOrWhiteSpace(profile.Experience))
        {
            slides.Add(new VideoSlide
            {
                Order = 3,
                Title = "Experience",
                Content = Truncate(profile.Experience, 200),
                Duration = 6,
                Type = SlideType.Content
            });
        }

        if (!string.IsNullOrWhiteSpace(profile.Skills))
        {
            var topSkills = profile.Skills
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
                    Title = "Skills",
                    Content = string.Join("  •  ", topSkills),
                    Duration = 5,
                    Type = SlideType.Skills
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.Education))
        {
            slides.Add(new VideoSlide
            {
                Order = 5,
                Title = "Education",
                Content = Truncate(profile.Education, 180),
                Duration = 4,
                Type = SlideType.Content
            });
        }

        slides.Add(new VideoSlide
        {
            Order = 6,
            Title = "Let's Connect!",
            Subtitle = profile.Email,
            Content = profile.LinkedInUrl ?? "",
            Duration = 4,
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

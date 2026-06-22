using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VidCV.Web.Models;

namespace VidCV.Web.Services;

/// <summary>
/// Connects to Ollama (free local AI) or any OpenAI-compatible endpoint.
/// Falls back to smart template-based generation if AI is unavailable.
/// No API keys, no subscriptions — runs 100% free.
/// </summary>
public class AiScriptService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AiScriptService> _logger;

    public AiScriptService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<AiScriptService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<string> GenerateScriptAsync(CvProfile profile)
    {
        var prompt = BuildPrompt(profile);

        // Try AI generation first
        try
        {
            var aiResult = await CallOllamaAsync(prompt);
            if (!string.IsNullOrWhiteSpace(aiResult) && aiResult.Length > 50)
            {
                _logger.LogInformation("AI script generated successfully ({Len} chars)", aiResult.Length);
                return CleanScript(aiResult);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("AI unavailable, using smart fallback: {Msg}", ex.Message);
        }

        // Smart fallback — no AI needed, still produces great scripts
        return GenerateSmartFallback(profile);
    }

    private string BuildPrompt(CvProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a professional video script writer. Write a compelling 30-second professional intro video script.");
        sb.AppendLine("The script should be EXACTLY 5-6 short paragraphs, each 1-2 sentences max.");
        sb.AppendLine("Format: Each paragraph becomes one video slide. Keep it punchy, confident, professional.");
        sb.AppendLine("Do NOT use bullet points. Do NOT include slide numbers or labels.");
        sb.AppendLine("Write in first person as if the person is introducing themselves.");
        sb.AppendLine();
        sb.AppendLine("Person's details:");
        sb.AppendLine($"Name: {profile.FullName}");

        if (!string.IsNullOrWhiteSpace(profile.JobTitle))
            sb.AppendLine($"Job Title: {profile.JobTitle}");
        if (!string.IsNullOrWhiteSpace(profile.Summary))
            sb.AppendLine($"Summary: {profile.Summary}");
        if (!string.IsNullOrWhiteSpace(profile.Skills))
            sb.AppendLine($"Skills: {profile.Skills}");
        if (!string.IsNullOrWhiteSpace(profile.Experience))
            sb.AppendLine($"Experience: {profile.Experience}");
        if (!string.IsNullOrWhiteSpace(profile.Education))
            sb.AppendLine($"Education: {profile.Education}");

        sb.AppendLine();
        sb.AppendLine("Write the script now (5-6 paragraphs, first person, professional tone):");

        return sb.ToString();
    }

    private async Task<string> CallOllamaAsync(string prompt)
    {
        var baseUrl = _config["AI:OllamaUrl"] ?? "http://localhost:11434";
        var model = _config["AI:Model"] ?? "llama3.2";

        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);

        var request = new
        {
            model,
            prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                top_p = 0.9,
                num_predict = 400
            }
        };

        var response = await client.PostAsJsonAsync($"{baseUrl}/api/generate", request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("response").GetString() ?? "";
    }

    private string GenerateSmartFallback(CvProfile profile)
    {
        var sections = new List<string>();

        // Slide 1 — Powerful intro
        var title = !string.IsNullOrWhiteSpace(profile.JobTitle)
            ? profile.JobTitle
            : "professional";
        sections.Add($"Hi, I'm {profile.FullName} — a passionate {title} dedicated to delivering exceptional results and driving innovation.");

        // Slide 2 — Summary / value proposition
        if (!string.IsNullOrWhiteSpace(profile.Summary))
        {
            var summary = Truncate(profile.Summary, 180);
            sections.Add(summary);
        }
        else
        {
            sections.Add($"With a strong track record in my field, I bring a unique combination of technical expertise and strategic thinking to every challenge I take on.");
        }

        // Slide 3 — Experience highlights
        if (!string.IsNullOrWhiteSpace(profile.Experience))
        {
            var exp = Truncate(profile.Experience, 180);
            sections.Add($"My experience spans {exp}");
        }
        else
        {
            sections.Add("I've built my career on a foundation of continuous learning, collaboration, and a commitment to excellence in everything I deliver.");
        }

        // Slide 4 — Skills showcase
        if (!string.IsNullOrWhiteSpace(profile.Skills))
        {
            var topSkills = profile.Skills
                .Split(new[] { ',', ';', '|', '\n', '•', '·' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 1 && s.Length < 40)
                .Take(6)
                .ToList();

            if (topSkills.Count > 0)
                sections.Add($"I specialize in {string.Join(", ", topSkills)} — bringing deep expertise to every project.");
        }

        // Slide 5 — Education
        if (!string.IsNullOrWhiteSpace(profile.Education))
        {
            var edu = Truncate(profile.Education, 150);
            sections.Add($"I hold credentials in {edu}");
        }

        // Slide 6 — Call to action
        var contactLine = !string.IsNullOrWhiteSpace(profile.Email)
            ? $"Reach me at {profile.Email}"
            : "Let's connect";
        sections.Add($"I'm always open to exciting new opportunities and collaborations. {contactLine} — I'd love to hear from you.");

        return string.Join("\n\n", sections);
    }

    private static string CleanScript(string raw)
    {
        var lines = raw.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Where(l => !l.StartsWith("Slide") && !l.StartsWith("#") && !l.StartsWith("*") && !l.StartsWith("-"))
            .ToList();

        return string.Join("\n\n", lines);
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Replace("\n", " ").Replace("\r", " ").Trim();
        while (text.Contains("  ")) text = text.Replace("  ", " ");
        return text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";
    }
}

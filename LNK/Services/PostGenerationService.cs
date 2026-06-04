using System.Text.Json;
using System.Text.RegularExpressions;
using LNK.Data;
using LNK.Helpers;
using LNK.Models;
using Microsoft.EntityFrameworkCore;

namespace LNK.Services;

public class PostGenerationService : IPostGenerationService
{
    private readonly ApplicationDbContext _db;
    private readonly IOllamaService _ollama;
    private readonly ILogger<PostGenerationService> _logger;

    public PostGenerationService(ApplicationDbContext db, IOllamaService ollama, ILogger<PostGenerationService> logger)
    {
        _db = db;
        _ollama = ollama;
        _logger = logger;
    }

    public async Task<Post> GenerateForUserAsync(ApplicationUser user, UserSettings settings, CancellationToken cancellationToken = default)
    {
        var lengthGuide = settings.PostLength switch
        {
            "Short" => "under 120 words",
            "Long" => "250-350 words",
            _ => "150-220 words"
        };

        const string jsonShape = "{\"title\":\"short internal title\",\"hook\":\"attention-grabbing first line\",\"content\":\"main body\",\"callToAction\":\"closing CTA line\",\"hashtags\":\"3-5 hashtags space-separated\"}";
        var prompt =
            $"You are an expert LinkedIn ghostwriter. Create ONE LinkedIn post for a professional in {settings.Industry}.\n" +
            $"Topics: {settings.Topics}\n" +
            $"Keywords to weave in: {settings.Keywords}\n" +
            $"Tone: {settings.Tone}\n" +
            $"Length: {lengthGuide}\n\n" +
            $"Return ONLY valid JSON with this exact structure (no markdown):\n{jsonShape}";

        var raw = await _ollama.GenerateAsync(prompt, cancellationToken);
        var parsed = ParseResponse(raw, settings);

        var post = new Post
        {
            UserId = user.Id,
            Title = parsed.Title,
            Hook = parsed.Hook,
            Content = parsed.Content,
            CallToAction = parsed.Cta,
            Hashtags = parsed.Hashtags,
            ImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid():N}/1200/630",
            Status = "Ready",
            GeneratedAt = DateTime.UtcNow,
            ScheduledFor = DateTime.UtcNow.Date.Add(settings.DailyPostTime)
        };
        post.FullText = PostFormatter.BuildFullText(post);

        _db.Posts.Add(post);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Generated post {PostId} for user {UserId}", post.Id, user.Id);
        return post;
    }

    private static (string Title, string Hook, string Content, string Cta, string Hashtags) ParseResponse(string raw, UserSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var jsonMatch = Regex.Match(raw, "\\{[\\s\\S]*\\}");
            if (jsonMatch.Success)
            {
                try
                {
                    var doc = JsonDocument.Parse(jsonMatch.Value);
                    var r = doc.RootElement;
                    return (
                        r.GetProperty("title").GetString() ?? "Daily Post",
                        r.GetProperty("hook").GetString() ?? "",
                        r.GetProperty("content").GetString()?.Replace("\\n", "\n") ?? "",
                        r.GetProperty("callToAction").GetString() ?? "",
                        r.GetProperty("hashtags").GetString() ?? "#LinkedIn #Growth"
                    );
                }
                catch { /* fallback */ }
            }
        }

        return (
            $"{settings.Tone} insight — {settings.Industry}",
            $"Here's what leaders in {settings.Industry} should know this week.",
            $"I've been exploring {settings.Topics} and one pattern keeps showing up: consistency beats complexity.\n\n"
            + $"Focus on one meaningful action today. Your network will notice the clarity.",
            "What's your take? Drop a comment below.",
            "#Leadership #Innovation #ProfessionalGrowth"
        );
    }
}

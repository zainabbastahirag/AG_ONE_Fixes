using System.Text.RegularExpressions;
using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

/// <summary>
/// Composes the shared layout with a per-email content fragment and substitutes tokens.
/// Brand images are referenced in templates with {{IMG:key}} and are either embedded as
/// base64 data-URIs (for standalone previews) or as cid: references (for real emails).
/// </summary>
public sealed class EmailTemplateRenderer
{
    /// <summary>Maps an image key (and its Content-ID) to the asset filename under Assets/.</summary>
    public static readonly IReadOnlyDictionary<string, string> AssetFiles = new Dictionary<string, string>
    {
        ["agone"] = "ag_one_logo.png",
        ["ball"] = "nexa_sphere.png",
        ["nexaword"] = "nexa_wordmark.png",
        ["footer"] = "footer.png",
        ["cardbg"] = "card_bg.png",
    };

    private static readonly Regex ImgToken = new(@"\{\{IMG:(\w+)\}\}", RegexOptions.Compiled);

    private readonly string _templatesDir;
    private readonly string _assetsDir;
    private readonly string _layout;
    private readonly string _cardStart;
    private readonly string _cardEnd;

    public EmailTemplateRenderer(string baseDir)
    {
        _templatesDir = Path.Combine(baseDir, "Templates");
        _assetsDir = Path.Combine(baseDir, "Assets");
        _layout = File.ReadAllText(Path.Combine(_templatesDir, "_layout.html"));
        _cardStart = File.ReadAllText(Path.Combine(_templatesDir, "_card_start.html"));
        _cardEnd = File.ReadAllText(Path.Combine(_templatesDir, "_card_end.html"));
    }

    public string AssetPath(string key) => Path.Combine(_assetsDir, AssetFiles[key]);

    /// <summary>
    /// Builds the full HTML body for an email.
    /// </summary>
    /// <param name="email">The campaign email metadata.</param>
    /// <param name="greeting">Greeting word shown as "Hi {greeting},".</param>
    /// <param name="feedbackUrl">Shareable feedback link for the launch email.</param>
    /// <param name="embedImageInline">
    /// When true, images are embedded as base64 data-URIs (great for standalone preview files).
    /// When false, <c>cid:</c> references are used so the sender can attach the real images.
    /// </param>
    public string Render(CampaignEmail email, string greeting, string feedbackUrl, bool embedImageInline, string? cardBackgroundUrl = null)
    {
        var content = File.ReadAllText(Path.Combine(_templatesDir, email.Template))
            .Replace("{{CARD_START}}", _cardStart)
            .Replace("{{CARD_END}}", _cardEnd);

        // The card gradient is delivered as a visible <img> (Gmail/mobile) plus VML/background
        // fallbacks (Outlook). A hosted https URL (if provided) is used everywhere.
        var cardBg = !string.IsNullOrWhiteSpace(cardBackgroundUrl)
            ? cardBackgroundUrl!
            : embedImageInline ? BuildDataUri(AssetPath("cardbg")) : "cid:cardbg";

        var html = _layout
            .Replace("{{PREHEADER}}", System.Net.WebUtility.HtmlEncode(email.Preheader))
            .Replace("{{CONTENT}}", content)
            .Replace("{{NAME}}", System.Net.WebUtility.HtmlEncode(greeting))
            .Replace("{{FEEDBACK_URL}}", feedbackUrl)
            .Replace("{{CARD_BG}}", cardBg);

        return ImgToken.Replace(html, m =>
        {
            var key = m.Groups[1].Value;
            if (!AssetFiles.ContainsKey(key)) return m.Value;
            if (key == "cardbg" && !string.IsNullOrWhiteSpace(cardBackgroundUrl))
                return cardBackgroundUrl;
            return embedImageInline ? BuildDataUri(AssetPath(key)) : $"cid:{key}";
        });
    }

    /// <summary>The inline images actually referenced (cid:) in the given HTML.</summary>
    public IReadOnlyList<InlineImage> InlineImagesFor(string html)
    {
        var list = new List<InlineImage>();
        foreach (var key in AssetFiles.Keys)
        {
            if (html.Contains($"cid:{key}") && File.Exists(AssetPath(key)))
                list.Add(new InlineImage(key, AssetPath(key)));
        }
        return list;
    }

    private static string BuildDataUri(string imagePath)
    {
        if (!File.Exists(imagePath)) return "";
        var bytes = File.ReadAllBytes(imagePath);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
}

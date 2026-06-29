using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

/// <summary>
/// Composes the shared layout with a per-email content fragment and substitutes tokens.
/// </summary>
public sealed class EmailTemplateRenderer
{
    public const string NexaImageContentId = "nexa_sphere";

    private readonly string _templatesDir;
    private readonly string _assetsDir;
    private readonly string _layout;

    public EmailTemplateRenderer(string baseDir)
    {
        _templatesDir = Path.Combine(baseDir, "Templates");
        _assetsDir = Path.Combine(baseDir, "Assets");
        _layout = File.ReadAllText(Path.Combine(_templatesDir, "_layout.html"));
    }

    public string NexaImagePath => Path.Combine(_assetsDir, "nexa_sphere.png");

    /// <summary>
    /// Builds the full HTML body for an email.
    /// </summary>
    /// <param name="email">The campaign email metadata.</param>
    /// <param name="recipientName">Resolved greeting name.</param>
    /// <param name="feedbackUrl">Shareable feedback link for the launch email.</param>
    /// <param name="embedImageInline">
    /// When true the Nexa image is embedded as a base64 data URI (great for standalone preview files).
    /// When false a <c>cid:</c> reference is used so the SMTP sender can attach the real image.
    /// </param>
    public string Render(CampaignEmail email, string recipientName, string feedbackUrl, bool embedImageInline)
    {
        var contentPath = Path.Combine(_templatesDir, email.Template);
        var content = File.ReadAllText(contentPath);

        var imageSrc = embedImageInline
            ? BuildDataUri(NexaImagePath)
            : $"cid:{NexaImageContentId}";

        content = content
            .Replace("{{NAME}}", System.Net.WebUtility.HtmlEncode(recipientName))
            .Replace("{{NEXA_IMG_SRC}}", imageSrc)
            .Replace("{{FEEDBACK_URL}}", feedbackUrl);

        return _layout
            .Replace("{{PREHEADER}}", System.Net.WebUtility.HtmlEncode(email.Preheader))
            .Replace("{{CONTENT}}", content);
    }

    private static string BuildDataUri(string imagePath)
    {
        if (!File.Exists(imagePath))
            return "";
        var bytes = File.ReadAllBytes(imagePath);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }
}

namespace NexaEmailBlast.Models;

public sealed class AppConfig
{
    public SmtpConfig Smtp { get; set; } = new();
    public GraphConfig Graph { get; set; } = new();
    public SenderConfig Sender { get; set; } = new();
    public RecipientsConfig Recipients { get; set; } = new();
    public FeedbackConfig Feedback { get; set; } = new();
    public BrandingConfig Branding { get; set; } = new();
    public SendingConfig Sending { get; set; } = new();
    public List<CampaignEmail> Campaign { get; set; } = new();
}

public sealed class SmtpConfig
{
    public string Host { get; set; } = "smtp.office365.com";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;

    /// <summary>
    /// Optional explicit security mode: "starttls", "ssl", "none" or "auto".
    /// When empty, falls back to <see cref="UseStartTls"/> (true =&gt; starttls, false =&gt; ssl).
    /// </summary>
    public string Security { get; set; } = "";

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 100;
}

public sealed class GraphConfig
{
    /// <summary>Microsoft Graph host, e.g. graph.microsoft.com.</summary>
    public string Host { get; set; } = "graph.microsoft.com";

    /// <summary>Azure AD / Entra tenant (GUID or domain) the app registration belongs to.</summary>
    public string TenantId { get; set; } = "";

    /// <summary>Application (client) ID of the app registration.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Client secret for the app registration (app-only auth).</summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>OAuth scope for client-credentials flow. Leave default for app-only.</summary>
    public string Scope { get; set; } = "https://graph.microsoft.com/.default";

    /// <summary>Save a copy of each sent message in the sender's Sent Items.</summary>
    public bool SaveToSentItems { get; set; } = true;

    public string BaseUrl => $"https://{(string.IsNullOrWhiteSpace(Host) ? "graph.microsoft.com" : Host.Trim())}/v1.0";
}

public sealed class SenderConfig
{
    public string Name { get; set; } = "Nexa";
    public string Email { get; set; } = "nexa@aventragroup.com";
}

public sealed class RecipientsConfig
{
    /// <summary>Production recipient — the real campaign target.</summary>
    public string ToName { get; set; } = "AG All Employee";
    public string ToEmail { get; set; } = "allemployee@aventragroup.com";

    /// <summary>Test recipient — used by the "Send TEST" actions.</summary>
    public string TestName { get; set; } = "Zain Abbas";
    public string TestEmail { get; set; } = "zain.abbas@aventragroup.com";

    /// <summary>Greeting word shown in the email body, e.g. "Hi Aventrian,".</summary>
    public string Greeting { get; set; } = "Aventrian";

    /// <summary>Optional addresses copied on every message (comma separated).</summary>
    public string Cc { get; set; } = "";
    public string Bcc { get; set; } = "";
}

public sealed class FeedbackConfig
{
    /// <summary>Shareable link rendered behind the word "feedback" in the launch email.</summary>
    public string Url { get; set; } = "https://forms.office.com/r/agone-marketplace-feedback";
}

public sealed class BrandingConfig
{
    /// <summary>
    /// Optional. Leave empty when using images from the Assets/ folder (default).
    /// Only set this if you host card_bg.png on a public https URL instead.
    /// </summary>
    public string CardBackgroundUrl { get; set; } = "";
}

public sealed class SendingConfig
{
    /// <summary>Delivery provider: "Graph" (Microsoft Graph API) or "Smtp".</summary>
    public string Provider { get; set; } = "Graph";

    /// <summary>When true, nothing is sent; messages are written to the preview folder instead.</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>Pause between individual recipients to stay friendly with the mail server.</summary>
    public int ThrottleMillisecondsBetweenEmails { get; set; } = 800;

    /// <summary>Folder where rendered .html previews are written.</summary>
    public string PreviewOutputFolder { get; set; } = "preview";
}

public sealed class CampaignEmail
{
    public string Key { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Preheader { get; set; } = "";
    public string Template { get; set; } = "";

    /// <summary>Local send time, e.g. 2026-06-30T18:00:00.</summary>
    public string SendAtLocal { get; set; } = "";

    public DateTime ParseSendAt() =>
        DateTime.TryParse(SendAtLocal, out var dt) ? dt : DateTime.MinValue;
}

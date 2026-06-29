namespace NexaEmailBlast.Models;

public sealed class AppConfig
{
    public SmtpConfig Smtp { get; set; } = new();
    public SenderConfig Sender { get; set; } = new();
    public RecipientsConfig Recipients { get; set; } = new();
    public FeedbackConfig Feedback { get; set; } = new();
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

public sealed class SenderConfig
{
    public string Name { get; set; } = "Nexa";
    public string Email { get; set; } = "nexa@aventragroup.com";
}

public sealed class RecipientsConfig
{
    /// <summary>Path to the CSV file holding the bulk recipient list.</summary>
    public string CsvPath { get; set; } = "recipients.csv";

    /// <summary>Used when the CSV is missing/empty so the campaign still has a target.</summary>
    public string DefaultToName { get; set; } = "AG All Employee";
    public string DefaultToEmail { get; set; } = "zain.abbas@aventragroup.com";

    /// <summary>Greeting word used when a recipient has no name (e.g. "Aventrian").</summary>
    public string GreetingFallback { get; set; } = "Aventrian";

    /// <summary>Optional addresses copied on every message (comma separated).</summary>
    public string Cc { get; set; } = "";
    public string Bcc { get; set; } = "";
}

public sealed class FeedbackConfig
{
    /// <summary>Shareable link rendered behind the word "feedback" in the launch email.</summary>
    public string Url { get; set; } = "https://forms.office.com/r/agone-marketplace-feedback";
}

public sealed class SendingConfig
{
    /// <summary>When true, nothing is sent over SMTP; messages are written to the preview folder instead.</summary>
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

using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

public sealed class CampaignRunner
{
    private readonly AppConfig _config;
    private readonly string _baseDir;
    private readonly EmailTemplateRenderer _renderer;
    private readonly List<Recipient> _recipients;

    public CampaignRunner(AppConfig config, string baseDir)
    {
        _config = config;
        _baseDir = baseDir;
        _renderer = new EmailTemplateRenderer(baseDir);
        _recipients = new List<Recipient>
        {
            new() { Email = _config.Recipients.ToEmail, Name = _config.Recipients.ToName },
        };
    }

    /// <summary>Config object — mutable at runtime so the interactive menu can toggle DryRun/provider etc.</summary>
    public AppConfig Config => _config;

    /// <summary>The production recipient(s) — i.e. AG All Employee.</summary>
    public IReadOnlyList<Recipient> Recipients => _recipients;

    /// <summary>The test recipient (e.g. zain.abbas@aventragroup.com).</summary>
    public Recipient TestRecipient => new() { Email = _config.Recipients.TestEmail, Name = _config.Recipients.TestName };

    public IReadOnlyList<CampaignEmail> Emails => _config.Campaign;

    /// <summary>When true, prints API-style request details and full stack traces on failure.</summary>
    public bool Verbose { get; set; }

    public CampaignEmail? GetEmail(string key) =>
        _config.Campaign.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        return Path.IsPathRooted(path) ? path : Path.Combine(_baseDir, path);
    }

    private string GreetingFor(Recipient r) => _config.Recipients.Greeting;

    public void PrintPlan()
    {
        Console.WriteLine();
        Console.WriteLine("==== Nexa Email Blast — Campaign Plan ====");
        Console.WriteLine($"Sender      : {_config.Sender.Name} <{_config.Sender.Email}>");
        Console.WriteLine($"Provider    : {EmailSenderFactory.Describe(_config)}");
        Console.WriteLine($"Endpoint    : {EmailSenderFactory.DescribeEndpoint(_config)}");
        Console.WriteLine($"DryRun      : {_config.Sending.DryRun}");
        Console.WriteLine($"Debug       : {Verbose}");
        Console.WriteLine($"To (live)   : {_config.Recipients.ToName} <{_config.Recipients.ToEmail}>");
        Console.WriteLine($"Test address: {_config.Recipients.TestName} <{_config.Recipients.TestEmail}>");
        Console.WriteLine($"Greeting    : Hi {_config.Recipients.Greeting},");
        Console.WriteLine($"Images      : embedded from Assets/ (no CDN required)");
        Console.WriteLine($"Feedback link: {_config.Feedback.BuildLink()}");
        Console.WriteLine("Schedule    :");
        foreach (var e in _config.Campaign)
            Console.WriteLine($"   - {e.Key,-8} {e.ParseSendAt():ddd dd MMM yyyy HH:mm}  \"{e.Subject}\"  [{e.Template}]");
        Console.WriteLine("==========================================");
        Console.WriteLine();
    }

    public void PrintRecipients()
    {
        Console.WriteLine();
        Console.WriteLine($"   Live : {_config.Recipients.ToName} <{_config.Recipients.ToEmail}>");
        Console.WriteLine($"   Test : {_config.Recipients.TestName} <{_config.Recipients.TestEmail}>");
        var cc = SplitAddresses(_config.Recipients.Cc);
        var bcc = SplitAddresses(_config.Recipients.Bcc);
        if (cc.Count > 0) Console.WriteLine($"   Cc   : {string.Join(", ", cc)}");
        if (bcc.Count > 0) Console.WriteLine($"   Bcc  : {string.Join(", ", bcc)}");
        Console.WriteLine();
    }

    /// <summary>Renders every campaign email to a self-contained HTML preview file.</summary>
    public void RenderPreviews()
    {
        var outDir = ResolvePath(_config.Sending.PreviewOutputFolder);
        Directory.CreateDirectory(outDir);

        foreach (var email in _config.Campaign)
        {
            var html = _renderer.Render(email, _config.Recipients.Greeting, _config.Feedback.BuildLink(), embedImageInline: true, _config.Branding.CardBackgroundUrl);
            var path = Path.Combine(outDir, $"{email.Key}.html");
            File.WriteAllText(path, html);
            Console.WriteLine($"[preview] {email.Key} -> {path}");
        }
    }

    // ---- High-level entry points -------------------------------------------------

    /// <summary>CLI compatibility: run the whole (or one) campaign, honouring the schedule unless ignored.</summary>
    public async Task RunAsync(bool ignoreSchedule, string? onlyKey)
    {
        var emails = SelectEmails(onlyKey);
        if (emails.Count == 0)
        {
            Console.WriteLine($"[run] No campaign emails matched '{onlyKey}'. Nothing to do.");
            return;
        }

        if (ignoreSchedule)
            await SendNowAsync(emails);
        else
            await RunScheduledAsync(emails);
    }

    /// <summary>Send the given emails immediately to all recipients.</summary>
    public async Task SendNowAsync(IReadOnlyList<CampaignEmail> emails)
    {
        if (!ValidateChannel()) return;
        foreach (var email in emails)
            await DispatchAsync(email, _recipients);
        Console.WriteLine("\n[run] Done.");
    }

    /// <summary>Wait until <paramref name="when"/>, then send the given emails to all recipients.</summary>
    public async Task SendAtAsync(DateTime when, IReadOnlyList<CampaignEmail> emails)
    {
        if (!ValidateChannel()) return;
        await WaitUntilAsync(when, "custom time");
        foreach (var email in emails)
            await DispatchAsync(email, _recipients);
        Console.WriteLine("\n[run] Done.");
    }

    /// <summary>Run the configured schedule: each email waits for its own SendAtLocal.</summary>
    public async Task RunScheduledAsync(IReadOnlyList<CampaignEmail> emails)
    {
        if (!ValidateChannel()) return;
        foreach (var email in emails)
        {
            await WaitUntilAsync(email.ParseSendAt(), email.Key);
            await DispatchAsync(email, _recipients);
        }
        Console.WriteLine("\n[run] Campaign finished.");
    }

    /// <summary>Send a single email to one explicit address (a test send). Always attempts real delivery.</summary>
    public async Task SendTestAsync(string toEmail, string? toName, CampaignEmail email)
    {
        var recipient = new Recipient { Email = toEmail, Name = toName };
        if (!recipient.IsValid)
        {
            Console.WriteLine($"[test] '{toEmail}' is not a valid email address.");
            return;
        }

        IEmailSender sender;
        try
        {
            sender = EmailSenderFactory.Create(_config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[test] Cannot create sender: {ex.Message}");
            return;
        }

        using (sender)
        {
            Console.WriteLine($"\n[test] Sending \"{email.Subject}\" ({email.Key}) to {toEmail} via {EmailSenderFactory.DescribeEndpoint(_config)}");
            var html = _renderer.Render(email, GreetingFor(recipient), _config.Feedback.BuildLink(), embedImageInline: false, _config.Branding.CardBackgroundUrl);
            LogRequest(email, recipient, html);
            try
            {
                await sender.SendAsync(recipient, email.Subject, html, _renderer.InlineImagesFor(html),
                    SplitAddresses(_config.Recipients.Cc), SplitAddresses(_config.Recipients.Bcc));
                Console.WriteLine($"   [ok]   test email delivered to {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   [FAIL] {toEmail} : {(Verbose ? ex.ToString() : ex.Message)}");
            }
        }
    }

    /// <summary>Print the exact request body that would be sent (Graph JSON or SMTP MIME) without sending.</summary>
    public async Task InspectAsync(CampaignEmail email, Recipient? recipient = null)
    {
        var r = recipient ?? _recipients[0];
        var html = _renderer.Render(email, GreetingFor(r), _config.Feedback.BuildLink(), embedImageInline: false, _config.Branding.CardBackgroundUrl);
        var cc = SplitAddresses(_config.Recipients.Cc);
        var bcc = SplitAddresses(_config.Recipients.Bcc);

        Console.WriteLine($"\n--- Inspect {email.Key} -> {r.Email} ---");
        Console.WriteLine($"Endpoint: {EmailSenderFactory.DescribeEndpoint(_config)}");

        var images = _renderer.InlineImagesFor(html);
        if (EmailSenderFactory.IsGraph(_config))
        {
            var msg = GraphEmailSender.BuildMessage(r, email.Subject, html, images, cc, bcc);
            var json = await GraphEmailSender.ToDebugJsonAsync(msg);
            Console.WriteLine("Request body (application/json):");
            Console.WriteLine(json);
        }
        else
        {
            var mime = SmtpEmailSender.BuildMimeMessage(_config.Sender, r, email.Subject, html, images, cc, bcc);
            Console.WriteLine("MIME headers:");
            Console.WriteLine($"  From   : {mime.From}");
            Console.WriteLine($"  To     : {mime.To}");
            if (mime.Cc.Count > 0) Console.WriteLine($"  Cc     : {mime.Cc}");
            if (mime.Bcc.Count > 0) Console.WriteLine($"  Bcc    : {mime.Bcc}");
            Console.WriteLine($"  Subject: {mime.Subject}");
            var mimeType = mime.Body?.ContentType?.MimeType ?? "multipart/related";
            Console.WriteLine($"  Body   : {mimeType} (html {html.Length} chars, inline images: {string.Join(", ", images.Select(i => i.ContentId))})");
        }
        Console.WriteLine("--- end inspect ---\n");
    }

    // ---- Core dispatch -----------------------------------------------------------

    private List<CampaignEmail> SelectEmails(string? onlyKey) =>
        _config.Campaign
            .Where(e => onlyKey is null || string.Equals(e.Key, onlyKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private bool ValidateChannel()
    {
        if (_config.Sending.DryRun) return true;
        try
        {
            using var probe = EmailSenderFactory.Create(_config);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[run] Cannot start sending: {ex.Message}");
            return false;
        }
    }

    private async Task DispatchAsync(CampaignEmail email, IReadOnlyList<Recipient> recipients)
    {
        Console.WriteLine($"\n[send] === {email.Key} :: \"{email.Subject}\" -> {recipients.Count} recipient(s) ===");

        if (_config.Sending.DryRun)
        {
            RenderDryRun(email, recipients);
            return;
        }

        var cc = SplitAddresses(_config.Recipients.Cc);
        var bcc = SplitAddresses(_config.Recipients.Bcc);

        using var sender = EmailSenderFactory.Create(_config);
        int ok = 0, fail = 0;
        foreach (var r in recipients)
        {
            try
            {
                var html = _renderer.Render(email, GreetingFor(r), _config.Feedback.BuildLink(), embedImageInline: false, _config.Branding.CardBackgroundUrl);
                LogRequest(email, r, html);
                await sender.SendAsync(r, email.Subject, html, _renderer.InlineImagesFor(html), cc, bcc);
                ok++;
                Console.WriteLine($"   [ok]   {r.Email}");
            }
            catch (Exception ex)
            {
                fail++;
                Console.WriteLine($"   [FAIL] {r.Email} : {(Verbose ? ex.ToString() : ex.Message)}");
            }

            if (_config.Sending.ThrottleMillisecondsBetweenEmails > 0)
                await Task.Delay(_config.Sending.ThrottleMillisecondsBetweenEmails);
        }
        Console.WriteLine($"[send] {email.Key} complete. Sent={ok} Failed={fail}");
    }

    private void LogRequest(CampaignEmail email, Recipient r, string html)
    {
        if (!Verbose) return;
        var images = string.Join("+", _renderer.InlineImagesFor(html).Select(i => i.ContentId));
        var cc = SplitAddresses(_config.Recipients.Cc).Count;
        var bcc = SplitAddresses(_config.Recipients.Bcc).Count;
        Console.WriteLine($"   [debug] {EmailSenderFactory.DescribeEndpoint(_config)}");
        Console.WriteLine($"   [debug] To={r.Email} greeting=\"{GreetingFor(r)}\" subject=\"{email.Subject}\" " +
                          $"htmlChars={html.Length} inlineImages=[{images}] cc={cc} bcc={bcc} saveToSent={_config.Graph.SaveToSentItems}");
    }

    private void RenderDryRun(CampaignEmail email, IReadOnlyList<Recipient> recipients)
    {
        var outDir = ResolvePath(_config.Sending.PreviewOutputFolder);
        Directory.CreateDirectory(outDir);
        var html = _renderer.Render(email, _config.Recipients.Greeting, _config.Feedback.BuildLink(), embedImageInline: true, _config.Branding.CardBackgroundUrl);
        var path = Path.Combine(outDir, $"{email.Key}.html");
        File.WriteAllText(path, html);
        Console.WriteLine($"   [DRY RUN] Would send to {recipients.Count} recipient(s). Preview written: {path}");
        foreach (var r in recipients.Take(10))
            Console.WriteLine($"             -> {r.Email}");
        if (recipients.Count > 10)
            Console.WriteLine($"             ... and {recipients.Count - 10} more");
    }

    private static async Task WaitUntilAsync(DateTime target, string label)
    {
        if (target == DateTime.MinValue)
        {
            Console.WriteLine($"[schedule] {label} has no valid time; sending immediately.");
            return;
        }

        while (true)
        {
            var remaining = target - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                Console.WriteLine($"[schedule] {label} due ({target:ddd dd MMM HH:mm}). Sending now.");
                return;
            }

            Console.WriteLine($"[schedule] Waiting for {label} at {target:ddd dd MMM HH:mm} (in {FormatRemaining(remaining)})...");
            var sleep = remaining > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(5) : remaining;
            await Task.Delay(sleep);
        }
    }

    private static string FormatRemaining(TimeSpan t) =>
        t.TotalDays >= 1
            ? $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m"
            : $"{t.Hours}h {t.Minutes}m {t.Seconds}s";

    private static List<string> SplitAddresses(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

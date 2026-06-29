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
        _recipients = ResolveRecipients();
    }

    public IReadOnlyList<Recipient> Recipients => _recipients;

    private List<Recipient> ResolveRecipients()
    {
        var csvPath = ResolvePath(_config.Recipients.CsvPath);
        var list = CsvRecipientReader.Read(csvPath);

        if (list.Count == 0)
        {
            Console.WriteLine($"[recipients] No CSV rows found at '{csvPath}'. Falling back to default recipient.");
            list.Add(new Recipient
            {
                Email = _config.Recipients.DefaultToEmail,
                Name = _config.Recipients.DefaultToName,
            });
        }
        else
        {
            Console.WriteLine($"[recipients] Loaded {list.Count} recipient(s) from '{csvPath}'.");
        }

        return list;
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        return Path.IsPathRooted(path) ? path : Path.Combine(_baseDir, path);
    }

    private string GreetingFor(Recipient r) =>
        string.IsNullOrWhiteSpace(r.Name) ? _config.Recipients.GreetingFallback : r.Name!;

    public void PrintPlan()
    {
        Console.WriteLine();
        Console.WriteLine("==== Nexa Email Blast — Campaign Plan ====");
        Console.WriteLine($"Sender      : {_config.Sender.Name} <{_config.Sender.Email}>");
        Console.WriteLine($"SMTP        : {_config.Smtp.Host}:{_config.Smtp.Port} (StartTls={_config.Smtp.UseStartTls})");
        Console.WriteLine($"DryRun      : {_config.Sending.DryRun}");
        Console.WriteLine($"Recipients  : {_recipients.Count}");
        Console.WriteLine($"Feedback URL: {_config.Feedback.Url}");
        Console.WriteLine("Schedule    :");
        foreach (var e in _config.Campaign)
            Console.WriteLine($"   - {e.Key,-8} {e.ParseSendAt():ddd dd MMM yyyy HH:mm}  \"{e.Subject}\"  [{e.Template}]");
        Console.WriteLine("==========================================");
        Console.WriteLine();
    }

    /// <summary>Renders every campaign email to a self-contained HTML preview file.</summary>
    public void RenderPreviews()
    {
        var outDir = ResolvePath(_config.Sending.PreviewOutputFolder);
        Directory.CreateDirectory(outDir);
        var sampleName = _recipients[0].Name ?? _config.Recipients.GreetingFallback;

        foreach (var email in _config.Campaign)
        {
            var html = _renderer.Render(email, sampleName, _config.Feedback.Url, embedImageInline: true);
            var path = Path.Combine(outDir, $"{email.Key}.html");
            File.WriteAllText(path, html);
            Console.WriteLine($"[preview] {email.Key} -> {path}");
        }
    }

    /// <summary>Runs the campaign: for each email, optionally wait for its scheduled time then send to all recipients.</summary>
    public async Task RunAsync(bool ignoreSchedule, string? onlyKey)
    {
        var emails = _config.Campaign
            .Where(e => onlyKey is null || string.Equals(e.Key, onlyKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (emails.Count == 0)
        {
            Console.WriteLine($"[run] No campaign emails matched '{onlyKey}'. Nothing to do.");
            return;
        }

        var cc = SplitAddresses(_config.Recipients.Cc);
        var bcc = SplitAddresses(_config.Recipients.Bcc);

        foreach (var email in emails)
        {
            if (!ignoreSchedule)
                await WaitUntilAsync(email);

            Console.WriteLine($"\n[send] === {email.Key} :: \"{email.Subject}\" -> {_recipients.Count} recipient(s) ===");

            if (_config.Sending.DryRun)
            {
                RenderDryRun(email);
                continue;
            }

            using var sender = new EmailSender(_config.Smtp, _config.Sender);
            int ok = 0, fail = 0;
            foreach (var r in _recipients)
            {
                try
                {
                    var html = _renderer.Render(email, GreetingFor(r), _config.Feedback.Url, embedImageInline: false);
                    await sender.SendAsync(r, email.Subject, html, _renderer.NexaImagePath, cc, bcc);
                    ok++;
                    Console.WriteLine($"   [ok]   {r.Email}");
                }
                catch (Exception ex)
                {
                    fail++;
                    Console.WriteLine($"   [FAIL] {r.Email} : {ex.Message}");
                }

                if (_config.Sending.ThrottleMillisecondsBetweenEmails > 0)
                    await Task.Delay(_config.Sending.ThrottleMillisecondsBetweenEmails);
            }
            Console.WriteLine($"[send] {email.Key} complete. Sent={ok} Failed={fail}");
        }

        Console.WriteLine("\n[run] Campaign finished.");
    }

    private void RenderDryRun(CampaignEmail email)
    {
        var outDir = ResolvePath(_config.Sending.PreviewOutputFolder);
        Directory.CreateDirectory(outDir);
        var html = _renderer.Render(email, _recipients[0].Name ?? _config.Recipients.GreetingFallback,
            _config.Feedback.Url, embedImageInline: true);
        var path = Path.Combine(outDir, $"{email.Key}.html");
        File.WriteAllText(path, html);
        Console.WriteLine($"   [DRY RUN] Would send to {_recipients.Count} recipient(s). Preview written: {path}");
        foreach (var r in _recipients.Take(10))
            Console.WriteLine($"             -> {r.Email}");
        if (_recipients.Count > 10)
            Console.WriteLine($"             ... and {_recipients.Count - 10} more");
    }

    private static async Task WaitUntilAsync(CampaignEmail email)
    {
        var target = email.ParseSendAt();
        if (target == DateTime.MinValue)
        {
            Console.WriteLine($"[schedule] {email.Key} has no valid SendAtLocal; sending immediately.");
            return;
        }

        while (true)
        {
            var now = DateTime.Now;
            var remaining = target - now;
            if (remaining <= TimeSpan.Zero)
            {
                Console.WriteLine($"[schedule] {email.Key} due ({target:ddd dd MMM HH:mm}). Sending now.");
                return;
            }

            Console.WriteLine($"[schedule] Waiting for {email.Key} at {target:ddd dd MMM HH:mm} " +
                              $"(in {FormatRemaining(remaining)})...");

            // Cap each sleep so the countdown updates periodically.
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

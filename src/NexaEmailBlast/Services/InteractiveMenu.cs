using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

/// <summary>
/// A simple interactive console (REPL) so you can send now, schedule, send a test,
/// inspect the outgoing request like an API call, and toggle debug/dry-run on the fly.
/// </summary>
public sealed class InteractiveMenu
{
    private readonly CampaignRunner _runner;
    private readonly AppConfig _config;

    public InteractiveMenu(CampaignRunner runner)
    {
        _runner = runner;
        _config = runner.Config;
    }

    public async Task RunAsync()
    {
        while (true)
        {
            PrintMenu();
            Console.Write("Choose an option: ");
            var choice = Console.ReadLine();
            if (choice is null)
            {
                Console.WriteLine("(no input — exiting)");
                return;
            }

            choice = choice.Trim().ToLowerInvariant();
            Console.WriteLine();

            switch (choice)
            {
                case "1": _runner.PrintPlan(); break;
                case "2": _runner.PrintRecipients(); break;
                case "3": _runner.RenderPreviews(); break;

                case "4":
                {
                    var email = PickEmail();
                    if (email is not null) await _runner.InspectAsync(email);
                    break;
                }

                case "5":
                    if (Confirm($"Send TEST of ALL {_runner.Emails.Count} emails to {_config.Recipients.TestEmail}?"))
                        foreach (var e in _runner.Emails)
                            await _runner.SendTestAsync(_config.Recipients.TestEmail, _config.Recipients.TestName, e);
                    break;

                case "6":
                {
                    var email = PickEmail();
                    if (email is null) break;
                    Console.Write($"Send test to which address [{_config.Recipients.TestEmail}]: ");
                    var addr = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(addr)) addr = _config.Recipients.TestEmail;
                    await _runner.SendTestAsync(addr.Trim(), null, email);
                    break;
                }

                case "7":
                    if (Confirm($"Send ALL {_runner.Emails.Count} emails now (LIVE) to {_config.Recipients.ToEmail}?"))
                        await _runner.SendNowAsync(_runner.Emails.ToList());
                    break;

                case "8":
                {
                    var email = PickEmail();
                    if (email is null) break;
                    if (Confirm($"Send '{email.Key}' now (LIVE) to {_config.Recipients.ToEmail}?"))
                        await _runner.SendNowAsync(new List<CampaignEmail> { email });
                    break;
                }

                case "9":
                {
                    var when = AskDateTime();
                    if (when is null) break;
                    var scope = AskAllOrOne();
                    if (scope.Count == 0) break;
                    Console.WriteLine($"Scheduled for {when:ddd dd MMM yyyy HH:mm} -> {_config.Recipients.ToEmail}. Keep this window open; press Ctrl+C to cancel.");
                    await _runner.SendAtAsync(when.Value, scope);
                    break;
                }

                case "s":
                    Console.WriteLine($"Running the configured schedule -> {_config.Recipients.ToEmail}. Keep this window open; press Ctrl+C to cancel.");
                    await _runner.RunScheduledAsync(_runner.Emails.ToList());
                    break;

                case "d":
                    _runner.Verbose = !_runner.Verbose;
                    Console.WriteLine($"Debug is now {(_runner.Verbose ? "ON" : "OFF")}.");
                    break;

                case "r":
                    _config.Sending.DryRun = !_config.Sending.DryRun;
                    Console.WriteLine($"DryRun is now {(_config.Sending.DryRun ? "ON (nothing will be sent)" : "OFF (LIVE sending)")}.");
                    break;

                case "p":
                    _config.Sending.Provider =
                        EmailSenderFactory.IsGraph(_config) ? "Smtp" : "Graph";
                    Console.WriteLine($"Provider is now {_config.Sending.Provider}. Endpoint: {EmailSenderFactory.DescribeEndpoint(_config)}");
                    break;

                case "q":
                case "quit":
                case "exit":
                    Console.WriteLine("Bye.");
                    return;

                default:
                    Console.WriteLine("Unknown option.");
                    break;
            }

            Console.WriteLine();
        }
    }

    private void PrintMenu()
    {
        Console.WriteLine("==========================================================");
        Console.WriteLine("  Nexa Email Blast — Interactive Console");
        Console.WriteLine("----------------------------------------------------------");
        Console.WriteLine($"  Provider : {EmailSenderFactory.Describe(_config)}");
        Console.WriteLine($"  Endpoint : {EmailSenderFactory.DescribeEndpoint(_config)}");
        Console.WriteLine($"  DryRun   : {_config.Sending.DryRun}      Debug: {_runner.Verbose}");
        Console.WriteLine($"  Live to  : {_config.Recipients.ToEmail}    Test to: {_config.Recipients.TestEmail}");
        Console.WriteLine("----------------------------------------------------------");
        Console.WriteLine("   1) Show plan & config");
        Console.WriteLine("   2) Show recipients");
        Console.WriteLine("   3) Preview emails (render HTML)");
        Console.WriteLine("   4) Inspect request (API-style dump, no send)");
        Console.WriteLine("   5) Send TEST  — ALL emails to the test address");
        Console.WriteLine("   6) Send TEST  — pick an email & address");
        Console.WriteLine("   7) Send LIVE  — ALL emails now");
        Console.WriteLine("   8) Send LIVE  — ONE email now");
        Console.WriteLine("   9) Send LIVE  — at a custom date/time");
        Console.WriteLine("   S) Send LIVE  — run the configured SCHEDULE");
        Console.WriteLine("   D) Toggle Debug      R) Toggle DryRun      P) Switch provider");
        Console.WriteLine("   Q) Quit");
        Console.WriteLine("==========================================================");
    }

    private CampaignEmail? PickEmail()
    {
        Console.WriteLine("Which email?");
        for (var i = 0; i < _runner.Emails.Count; i++)
        {
            var e = _runner.Emails[i];
            Console.WriteLine($"   {i + 1}) {e.Key,-8} \"{e.Subject}\"");
        }
        Console.Write("Enter number (or key like email2): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return null;

        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= _runner.Emails.Count)
            return _runner.Emails[idx - 1];

        var byKey = _runner.GetEmail(input);
        if (byKey is null) Console.WriteLine($"No email matched '{input}'.");
        return byKey;
    }

    private static DateTime? AskDateTime()
    {
        Console.Write("Enter date & time (e.g. 2026-06-30 18:00): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (DateTime.TryParse(input, out var dt)) return dt;
        Console.WriteLine($"Could not parse '{input}' as a date/time.");
        return null;
    }

    private List<CampaignEmail> AskAllOrOne()
    {
        Console.Write("Send (A)ll emails or (O)ne? [A]: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (input == "o")
        {
            var email = PickEmail();
            return email is null ? new List<CampaignEmail>() : new List<CampaignEmail> { email };
        }
        return _runner.Emails.ToList();
    }

    private static bool Confirm(string question)
    {
        Console.Write($"{question} (y/N): ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        return input is "y" or "yes";
    }
}

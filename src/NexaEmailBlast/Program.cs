using Microsoft.Extensions.Configuration;
using NexaEmailBlast.Models;
using NexaEmailBlast.Services;

var baseDir = AppContext.BaseDirectory;

var configuration = new ConfigurationBuilder()
    .SetBasePath(baseDir)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "NEXA_")
    .Build();

var appConfig = configuration.Get<AppConfig>() ?? new AppConfig();

var command = (args.FirstOrDefault() ?? "help").ToLowerInvariant();
var ignoreSchedule = args.Contains("--now", StringComparer.OrdinalIgnoreCase);
var onlyKey = GetOption(args, "--email");

PrintBanner();

var runner = new CampaignRunner(appConfig, baseDir);

switch (command)
{
    case "preview":
        runner.PrintPlan();
        runner.RenderPreviews();
        Console.WriteLine("\nOpen the .html files in the preview folder to review the designs.");
        break;

    case "plan":
    case "list":
        runner.PrintPlan();
        break;

    case "send":
        runner.PrintPlan();
        if (!appConfig.Sending.DryRun)
            Console.WriteLine("LIVE MODE: emails will be delivered over SMTP.\n");
        else
            Console.WriteLine("DRY RUN: no emails will be delivered (set Sending.DryRun=false to go live).\n");
        await runner.RunAsync(ignoreSchedule, onlyKey);
        break;

    default:
        PrintHelp();
        break;
}

return 0;

static string? GetOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

static void PrintBanner()
{
    Console.WriteLine("======================================================");
    Console.WriteLine("  Nexa Email Blast — AG ONE Marketplace Campaign");
    Console.WriteLine("======================================================");
}

static void PrintHelp()
{
    Console.WriteLine(@"
Usage: dotnet run -- <command> [options]

Commands:
  preview            Render all campaign emails to self-contained HTML files
                     in the preview folder (no sending).
  plan | list        Print the campaign schedule, recipient count and config.
  send               Run the campaign. Waits for each email's scheduled time,
                     then sends to every recipient from the CSV.

Options:
  --now              Ignore the schedule and send immediately.
  --email <key>      Only process a single campaign email (e.g. email1).

Examples:
  dotnet run -- preview
  dotnet run -- plan
  dotnet run -- send                 # follows the configured schedule
  dotnet run -- send --now           # send everything right now
  dotnet run -- send --now --email email4

Configuration lives in appsettings.json. Set Sending.DryRun=false and provide
SMTP credentials to deliver real emails. The SMTP password may also be supplied
via the NEXA_Smtp__Password environment variable.
");
}

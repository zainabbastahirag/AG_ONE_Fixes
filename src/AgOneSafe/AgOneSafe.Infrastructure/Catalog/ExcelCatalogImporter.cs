using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace AgOneSafe.Infrastructure.Catalog;

public sealed record CatalogImportResult(
    int RowsRead,
    int ControlsImported,
    int AutomatedAudits,
    int ManualAudits,
    IReadOnlyList<string> Warnings);

public interface IExcelCatalogImporter
{
    /// <summary>
    /// Reads a CIS benchmark workbook - the "Vendor Reference / Level / Title / Audit by /
    /// Remediation / User impact / CIS Control / NIST CSF" layout - into the catalog.
    /// </summary>
    Task<CatalogImportResult> ImportAsync(
        Stream workbook,
        string benchmark,
        string version,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bridges the security team's spreadsheet and the execution engine. Metadata and the verbatim
/// procedures come across automatically; a single-pipeline PowerShell audit is promoted to an
/// executable action, and anything else is parked as a manual control so it shows up in the
/// roadmap instead of silently passing. Assertions are authored afterwards in the catalog editor.
/// </summary>
public sealed class ExcelCatalogImporter : IExcelCatalogImporter
{
    private static readonly Regex PowerShellCommand =
        new(@"^\s*(Get|Test|Resolve|Measure)-[A-Za-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] ReferenceHeaders = { "vendor reference", "vendor referen", "reference", "control id", "id" };
    private static readonly string[] LevelHeaders = { "level", "profile", "profile level" };
    private static readonly string[] TitleHeaders = { "title", "control title", "recommendation" };
    private static readonly string[] AuditHeaders = { "audit by", "audit", "audit procedure" };
    private static readonly string[] RemediationHeaders = { "remediation", "remediation steps", "fix" };
    private static readonly string[] ImpactHeaders = { "user impact", "impact" };
    private static readonly string[] CisHeaders = { "cis critical security control", "cis control", "cis safeguard" };
    private static readonly string[] NistHeaders = { "nist csf core area", "nist csf", "nist" };
    private static readonly string[] DomainHeaders = { "domain", "section", "service" };

    private readonly ICatalogLoader _loader;
    private readonly ILogger<ExcelCatalogImporter> _logger;

    public ExcelCatalogImporter(ICatalogLoader loader, ILogger<ExcelCatalogImporter> logger)
    {
        _loader = loader;
        _logger = logger;
    }

    public async Task<CatalogImportResult> ImportAsync(
        Stream workbook,
        string benchmark,
        string version,
        CancellationToken cancellationToken = default)
    {
        using var book = new XLWorkbook(workbook);
        var sheet = book.Worksheets.First();

        var headerRow = sheet.FirstRowUsed()
                        ?? throw new InvalidOperationException("The worksheet is empty.");

        var columns = MapColumns(headerRow);

        if (!columns.ContainsKey("reference") || !columns.ContainsKey("title"))
        {
            throw new InvalidOperationException(
                "The worksheet must contain at least a 'Vendor Reference' and a 'Title' column.");
        }

        var warnings = new List<string>();
        var document = new CatalogDocument { Benchmark = benchmark, Version = version };
        var rowsRead = 0;

        foreach (var row in sheet.RowsUsed().Skip(headerRow.RowNumber()))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reference = Read(row, columns, "reference");
            var title = Read(row, columns, "title");

            if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            rowsRead++;

            var auditText = Read(row, columns, "audit");
            var remediationText = Read(row, columns, "remediation");
            var level = NormaliseLevel(Read(row, columns, "level"));

            var control = new CatalogControl
            {
                Reference = reference.Trim(),
                Title = title.Trim(),
                Level = level,
                Domain = string.IsNullOrWhiteSpace(Read(row, columns, "domain"))
                    ? InferDomain(benchmark, reference)
                    : Read(row, columns, "domain"),
                UserImpact = string.IsNullOrWhiteSpace(Read(row, columns, "impact"))
                    ? "No Impact"
                    : Read(row, columns, "impact"),
                CisControl = Read(row, columns, "cis"),
                NistFunction = NormaliseNist(Read(row, columns, "nist")),
                AuditProcedure = auditText,
                RemediationProcedure = remediationText,
                Severity = level == "L1" ? "High" : "Medium",
                RiskWeight = level == "L1" ? 7 : 4
            };

            var executable = ExtractExecutableCommand(auditText);

            if (executable is not null)
            {
                control.AutomatedAudit = true;
                control.Audit = new CatalogAction
                {
                    Name = $"Audit {control.Reference}",
                    Executor = "PowerShell",
                    Module = InferModule(executable),
                    Payload = $"{executable} | ConvertTo-Json -Depth 5 -Compress"
                };

                warnings.Add(
                    $"{control.Reference}: audit command imported, but no assertion rules exist yet. " +
                    "Add them in the catalog editor before the control can return a pass or fail.");
            }
            else
            {
                control.AutomatedAudit = false;
                control.Audit = new CatalogAction
                {
                    Name = $"Manual audit {control.Reference}",
                    Executor = "Manual",
                    Module = "None",
                    Payload = auditText
                };
            }

            control.AutomatedRemediation = false;
            document.Controls.Add(control);
        }

        var imported = await _loader.UpsertAsync(document, cancellationToken);

        _logger.LogInformation("Imported {Count} controls from workbook for benchmark {Benchmark}", imported, benchmark);

        return new CatalogImportResult(
            rowsRead,
            imported,
            document.Controls.Count(c => c.AutomatedAudit),
            document.Controls.Count(c => !c.AutomatedAudit),
            warnings);
    }

    private static Dictionary<string, int> MapColumns(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var header = cell.GetString().Trim().ToLowerInvariant();

            if (Matches(header, ReferenceHeaders)) map.TryAdd("reference", cell.Address.ColumnNumber);
            else if (Matches(header, LevelHeaders)) map.TryAdd("level", cell.Address.ColumnNumber);
            else if (Matches(header, TitleHeaders)) map.TryAdd("title", cell.Address.ColumnNumber);
            else if (Matches(header, AuditHeaders)) map.TryAdd("audit", cell.Address.ColumnNumber);
            else if (Matches(header, RemediationHeaders)) map.TryAdd("remediation", cell.Address.ColumnNumber);
            else if (Matches(header, ImpactHeaders)) map.TryAdd("impact", cell.Address.ColumnNumber);
            else if (Matches(header, CisHeaders)) map.TryAdd("cis", cell.Address.ColumnNumber);
            else if (Matches(header, NistHeaders)) map.TryAdd("nist", cell.Address.ColumnNumber);
            else if (Matches(header, DomainHeaders)) map.TryAdd("domain", cell.Address.ColumnNumber);
        }

        return map;
    }

    private static bool Matches(string header, IEnumerable<string> candidates) =>
        candidates.Any(c => header.StartsWith(c, StringComparison.OrdinalIgnoreCase));

    private static string Read(IXLRow row, IReadOnlyDictionary<string, int> columns, string key) =>
        columns.TryGetValue(key, out var column) ? row.Cell(column).GetString().Trim() : string.Empty;

    /// <summary>
    /// Pulls the first runnable read-only pipeline out of the "Audit by" cell. Cells that describe
    /// portal navigation, or that mix prose with commands, stay manual on purpose - guessing there
    /// produces controls that appear automated but never actually assert anything.
    /// </summary>
    internal static string? ExtractExecutableCommand(string auditText)
    {
        if (string.IsNullOrWhiteSpace(auditText))
        {
            return null;
        }

        var lines = auditText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => PowerShellCommand.IsMatch(l))
            .ToList();

        if (lines.Count != 1)
        {
            return null;
        }

        var command = lines[0];

        // A "| fl Prop1,Prop2" tail formats for a human; Select-Object keeps the same fields
        // as real objects so ConvertTo-Json can serialise them.
        command = Regex.Replace(command, @"\|\s*(fl|format-list|ft|format-table)\s+", "| Select-Object ",
            RegexOptions.IgnoreCase);

        return command.Trim();
    }

    private static string InferModule(string command) => command switch
    {
        _ when command.Contains("-Cs", StringComparison.OrdinalIgnoreCase) => "MicrosoftTeams",
        _ when command.Contains("-EXO", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("Mailbox", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("Organization", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("Transport", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("Hosted", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("SafeLinks", StringComparison.OrdinalIgnoreCase) ||
               command.Contains("AntiPhish", StringComparison.OrdinalIgnoreCase) => "ExchangeOnline",
        _ when command.Contains("-SPO", StringComparison.OrdinalIgnoreCase) => "SharePointOnline",
        _ => "MicrosoftGraph"
    };

    private static string InferDomain(string benchmark, string reference)
    {
        var section = reference.Split('.').FirstOrDefault() ?? string.Empty;

        if (string.Equals(benchmark, "Azure", StringComparison.OrdinalIgnoreCase))
        {
            return section switch
            {
                "1" => "Identity",
                "2" => "Defender for Cloud",
                "3" => "Storage",
                "4" => "Database",
                "5" => "Logging & Monitoring",
                "6" => "Networking",
                "7" => "Compute",
                "8" => "Key Vault",
                "9" => "App Service",
                _ => "Azure"
            };
        }

        return section switch
        {
            "1" => "Microsoft 365 Admin",
            "2" => "Defender",
            "3" => "Purview",
            "5" => "Identity",
            "6" => "Exchange Online",
            "7" => "SharePoint & OneDrive",
            "8" => "Teams",
            "9" => "Fabric",
            _ => "Microsoft 365"
        };
    }

    private static string NormaliseLevel(string value) =>
        value.Contains('2') ? "L2" : "L1";

    private static string NormaliseNist(string value)
    {
        var normalised = value.Trim().ToLowerInvariant();

        return normalised switch
        {
            _ when normalised.StartsWith("gov") => "Govern",
            _ when normalised.StartsWith("ident") => "Identify",
            _ when normalised.StartsWith("prot") => "Protect",
            _ when normalised.StartsWith("det") => "Detect",
            _ when normalised.StartsWith("resp") => "Respond",
            _ when normalised.StartsWith("rec") => "Recover",
            _ => "Protect"
        };
    }
}

using AgoneSentimentSales.Core.Entities;
using AgoneSentimentSales.Core.Enums;
using AgoneSentimentSales.Core.Interfaces;
using ClosedXML.Excel;

namespace AgoneSentimentSales.Infrastructure.Services;

public class ExcelExportService : IExcelExportService
{
    private const string DarkBlue = "#2E5496";
    private const string MediumBlue = "#4472C4";
    private const string LightBlueRow = "#DEEAF6";
    private const string GreenMaturity = "#C6EFCE";
    private const string GreenText = "#006100";
    private const string OrangePartial = "#FFEB9C";
    private const string OrangeText = "#9C6500";
    private const string GreenConfirmed = "#C6EFCE";

    public Task<byte[]> ExportWorkbookAsync(IReadOnlyList<LseCompany> companies, CancellationToken cancellationToken = default)
    {
        using var wb = BuildWorkbook(companies);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public async Task<string> SaveWorkbookAsync(IReadOnlyList<LseCompany> companies, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var fileName = $"LSE_TOP100_IT_OFFSHORING_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        var path = Path.Combine(outputDirectory, fileName);
        using var wb = BuildWorkbook(companies);
        wb.SaveAs(path);
        return await Task.FromResult(path);
    }

    private static XLWorkbook BuildWorkbook(IReadOnlyList<LseCompany> companies)
    {
        var wb = new XLWorkbook();
        BuildDashboardSheet(wb, companies);
        BuildCompanyProfilesSheet(wb, companies);
        BuildItBudgetSheet(wb, companies);
        BuildTechnologyStrategySheet(wb, companies);
        BuildExecutiveContactsSheet(wb, companies);
        BuildOutsourcingPartnersSheet(wb, companies);
        BuildLeadGenerationSheet(wb, companies);
        return wb;
    }

    private static void BuildDashboardSheet(XLWorkbook wb, IReadOnlyList<LseCompany> companies)
    {
        var ws = wb.Worksheets.Add("LSE Dashboard Summary");
        ApplyTitle(ws, 1, 7, "LSE TOP 100 - IT OFFSHORING MARKET RESEARCH DASHBOARD");
        ApplySubtitle(ws, 2, 7, $"Research Date: {DateTime.UtcNow:MMMM yyyy} | Focus: IT Offshoring to India/Asia | London Stock Exchange Listed Companies");

        var confirmed = companies.Count(c => c.OffshoringStatus == OffshoringStatus.Confirmed);
        var partial = companies.Count(c => c.OffshoringStatus == OffshoringStatus.Partial);
        var totalItB = companies.Where(c => c.ItBudget != null).Sum(c => c.ItBudget!.EstimatedItBudgetGbpM) / 1000m;
        var offshoreB = companies.Where(c => c.ItBudget != null).Sum(c => c.ItBudget!.OffshoreResourceCostGbpM) / 1000m;
        var india = companies.Count(c => c.PrimaryOffshoreLocations.Contains("India", StringComparison.OrdinalIgnoreCase));

        var row = 4;
        ApplySectionHeader(ws, row, "KEY STATISTICS");
        row++;
        WriteMetric(ws, row++, "Total Companies Profiled", companies.Count.ToString(), "");
        WriteMetric(ws, row++, "Companies with Confirmed IT Offshoring", confirmed.ToString(), $"{(companies.Count > 0 ? confirmed * 100 / companies.Count : 0)}%");
        WriteMetric(ws, row++, "Companies with Partial Offshoring", partial.ToString(), "");
        WriteMetric(ws, row++, "Total Estimated IT Budget (Annual)", $"£{totalItB:N1}B+", "");
        WriteMetric(ws, row++, "Total Estimated Offshore IT Spend", $"£{offshoreB:N1}B", offshoreB > 0 && totalItB > 0 ? $"~{(offshoreB / totalItB * 100):N0}% of IT Budget" : "");
        WriteMetric(ws, row++, "Companies with India Operations", india.ToString(), $"{(companies.Count > 0 ? india * 100 / companies.Count : 0)}%");

        row += 2;
        ApplySectionHeader(ws, row, "SECTOR BREAKDOWN");
        row++;
        WriteTableHeader(ws, row, ["Sector", "# Companies", "Est. IT Budget (£B)", "Avg IT as % Revenue"]);
        row++;
        foreach (var g in companies.GroupBy(c => c.Sector).OrderByDescending(g => g.Sum(x => x.ItBudget?.EstimatedItBudgetGbpM ?? 0)))
        {
            var avgPct = g.Where(x => x.ItBudget != null).Select(x => x.ItBudget!.ItAsPercentOfRevenue).DefaultIfEmpty(0).Average();
            var budgetB = g.Sum(x => x.ItBudget?.EstimatedItBudgetGbpM ?? 0) / 1000m;
            WriteDataRow(ws, row++, [g.Key, g.Count().ToString(), budgetB.ToString("N1"), avgPct.ToString("N2") + "%"], row % 2 == 0);
        }

        ws.Column(1).Width = 42;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 22;
        ws.Column(4).Width = 22;
    }

    private static void BuildCompanyProfilesSheet(XLWorkbook wb, IReadOnlyList<LseCompany> companies)
    {
        var ws = wb.Worksheets.Add("LSE Company Profiles");
        ApplyTitle(ws, 1, 10, "LSE TOP 100 COMPANIES - IT OFFSHORING MARKET RESEARCH");
        ApplySubtitle(ws, 2, 10, $"Research Date: {DateTime.UtcNow:MMMM yyyy} | Focus: IT Offshoring to India/Asia | LSE Listed Companies");

        var headers = new[] { "Rank", "Company Name", "LSE Ticker", "Sector", "Market Cap (£B)", "HQ Location", "IT Offshoring Status", "Primary Offshore Locations", "Asia Subsidiary", "Notes" };
        var row = 4;
        WriteTableHeader(ws, row, headers);
        row++;
        foreach (var c in companies.OrderBy(x => x.Rank))
        {
            var cells = new[]
            {
                c.Rank.ToString(), c.CompanyName, c.Ticker, c.Sector,
                c.MarketCapGbpB.ToString("N1"), c.HqLocation,
                c.OffshoringStatus.ToString(), c.PrimaryOffshoreLocations,
                c.HasAsiaSubsidiary ? "Yes" : "No", c.Notes
            };
            WriteDataRow(ws, row, cells, row % 2 == 0);
            ApplyOffshoringStatusColor(ws, row, 7, c.OffshoringStatus);
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildItBudgetSheet(XLWorkbook wb, IReadOnlyList<LseCompany> companies)
    {
        var ws = wb.Worksheets.Add("LSE IT Budget Breakdown");
        ApplyTitle(ws, 1, 12, "IT BUDGET BREAKDOWN - LSE TOP 100 COMPANIES");
        ApplySubtitle(ws, 2, 12, "Note: IT budget data estimated based on industry benchmarks (2-8% of revenue; higher for financial services)");

        var headers = new[]
        {
            "Company", "Sector", "Annual Revenue (£B)", "Est. IT Budget (£M)", "IT as % Revenue",
            "CapEx (£M)", "OpEx (£M)", "Offshore Cost (£M)", "Onshore Cost (£M)", "Cloud (£M)",
            "Licensing (£M)", "App Support (£M)"
        };
        var row = 4;
        WriteTableHeader(ws, row, headers);
        row++;
        foreach (var c in companies.OrderBy(x => x.Rank))
        {
            var b = c.ItBudget;
            if (b == null) continue;
            WriteDataRow(ws, row, [
                c.CompanyName, c.Sector, b.AnnualRevenueGbpB.ToString("N1"), b.EstimatedItBudgetGbpM.ToString("N0"),
                b.ItAsPercentOfRevenue.ToString("N2") + "%", b.CapexGbpM.ToString("N0"), b.OpexGbpM.ToString("N0"),
                b.OffshoreResourceCostGbpM.ToString("N0"), b.OnshoreResourceCostGbpM.ToString("N0"),
                b.CloudInfrastructureGbpM.ToString("N0"), b.ApplicationLicensingGbpM.ToString("N0"),
                b.ApplicationSupportGbpM.ToString("N0")
            ], row % 2 == 0);
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildTechnologyStrategySheet(XLWorkbook wb, IReadOnlyList<LseCompany> companies)
    {
        var ws = wb.Worksheets.Add("LSE Technology Strategy");
        ApplyTitle(ws, 1, 7, "TECHNOLOGY STRATEGY & DIGITAL TRANSFORMATION - LSE TOP 100");

        var headers = new[] { "Company", "Digital Maturity", "AI/ML Programs", "Cloud Strategy", "Key Tech Initiatives", "Automation Focus", "Data Analytics" };
        var row = 3;
        WriteTableHeader(ws, row, headers);
        row++;
        foreach (var c in companies.OrderBy(x => x.Rank))
        {
            var t = c.TechnologyStrategy;
            if (t == null) continue;
            WriteDataRow(ws, row, [
                c.CompanyName, t.DigitalMaturity.ToString(), t.AiMlPrograms, t.CloudStrategy,
                t.KeyTechInitiatives, t.AutomationFocus, t.DataAnalytics
            ], row % 2 == 0);
            var maturityCell = ws.Cell(row, 2);
            if (t.DigitalMaturity >= DigitalMaturity.Advanced)
            {
                maturityCell.Style.Fill.BackgroundColor = XLColor.FromHtml(GreenMaturity);
                maturityCell.Style.Font.FontColor = XLColor.FromHtml(GreenText);
            }
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildExecutiveContactsSheet(XLWorkbook wb, IReadOnlyList<LseCompany> companies)
    {
        var ws = wb.Worksheets.Add("LSE Executive Contacts");
        ApplyTitle(ws, 1, 7, "EXECUTIVE CONTACTS - IT & TECHNOLOGY LEADERSHIP - LSE TOP 100");
        ApplySubtitle(ws, 2, 7, "Note: Contact information for professional outreach only. Verify via LinkedIn and company websites before use.");

        var headers = new[] { "Company", "Executive Name", "Title/Role", "LinkedIn Profile URL", "Estimated Email Format", "Location", "Areas of Responsibility" };
        var row = 4;
        WriteTableHeader(ws, row, headers);
        row++;
        foreach (var c in companies.OrderBy(x => x.CompanyName))
        {
            foreach (var e in c.ExecutiveContacts)
            {
                WriteDataRow(ws, row, [
                    c.CompanyName, e.ExecutiveName, e.Title, e.LinkedInUrl,
                    e.EstimatedEmailFormat, e.Location, e.AreasOfResponsibility
                ], row % 2 == 0);
                row++;
            }
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildOutsourcingPartnersSheet(XLWorkbook wb, IReadOnlyList<LseCompany> companies)
    {
        var ws = wb.Worksheets.Add("LSE Outsourcing Partners");
        ApplyTitle(ws, 1, 7, "OUTSOURCING PARTNERS - IT SERVICE PROVIDERS - LSE TOP 100");

        var headers = new[] { "Company", "Primary IT Outsourcing Partners", "Secondary Partners", "Offshore Delivery Centers", "Contract Type", "Est. Annual Contract (£M)", "Partnership Duration" };
        var row = 4;
        WriteTableHeader(ws, row, headers);
        row++;
        foreach (var c in companies.OrderBy(x => x.CompanyName))
        {
            var p = c.OutsourcingPartner;
            if (p == null) continue;
            WriteDataRow(ws, row, [
                c.CompanyName, p.PrimaryPartners, p.SecondaryPartners, p.OffshoreDeliveryCenters,
                p.ContractType, p.EstimatedAnnualContractGbpM?.ToString("N0") ?? "N/A", p.PartnershipDuration
            ], row % 2 == 0);
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildLeadGenerationSheet(XLWorkbook wb, IReadOnlyList<LseCompany> companies)
    {
        var ws = wb.Worksheets.Add("LSE Lead Generation Data");
        ApplyTitle(ws, 1, 7, "LEAD GENERATION DATA - LSE TOP 100");

        var headers = new[] { "Company", "Asia Operations", "IT Announcements", "Hiring", "Digital Roles", "Pain Points", "Renewal" };
        var row = 3;
        WriteTableHeader(ws, row, headers);
        row++;
        foreach (var c in companies.OrderBy(x => x.CompanyName))
        {
            var l = c.LeadGeneration;
            if (l == null) continue;
            WriteDataRow(ws, row, [
                c.CompanyName, l.AsiaOperations, l.ItAnnouncements, l.HiringTrends,
                l.DigitalRoles, l.PainPoints, l.RenewalCycle
            ], row % 2 == 0);
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void ApplyTitle(IXLWorksheet ws, int colCount, string title)
    {
        var range = ws.Range(1, 1, 2, colCount);
        range.Merge();
        range.Value = title;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(DarkBlue);
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 14;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void ApplyTitle(IXLWorksheet ws, int row, int colCount, string title) => ApplyTitle(ws, colCount, title);

    private static void ApplySubtitle(IXLWorksheet ws, int row, int colCount, string text)
    {
        var range = ws.Range(row, 1, row, colCount);
        range.Merge();
        range.Value = text;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(MediumBlue);
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Italic = true;
        range.Style.Font.FontSize = 10;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void ApplySectionHeader(IXLWorksheet ws, int row, string text)
    {
        ws.Cell(row, 1).Value = text;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(DarkBlue);
        ws.Cell(row, 1).Style.Font.FontSize = 12;
    }

    private static void WriteMetric(IXLWorksheet ws, int row, string metric, string value, string pct)
    {
        ws.Cell(row, 1).Value = metric;
        ws.Cell(row, 2).Value = value;
        ws.Cell(row, 3).Value = pct;
        ws.Range(row, 1, row, 3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private static void WriteTableHeader(IXLWorksheet ws, int row, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(MediumBlue);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Font.Bold = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.WrapText = true;
        }
    }

    private static void WriteDataRow(IXLWorksheet ws, int row, string[] values, bool alt)
    {
        for (var i = 0; i < values.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = values[i];
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            if (alt)
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(LightBlueRow);
        }
    }

    private static void ApplyOffshoringStatusColor(IXLWorksheet ws, int row, int col, OffshoringStatus status)
    {
        var cell = ws.Cell(row, col);
        switch (status)
        {
            case OffshoringStatus.Confirmed:
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(GreenConfirmed);
                break;
            case OffshoringStatus.Partial:
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(OrangePartial);
                cell.Style.Font.FontColor = XLColor.FromHtml(OrangeText);
                break;
        }
    }
}

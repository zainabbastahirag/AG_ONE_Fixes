using AgoneSentimentSales.Domain.Enums;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Infrastructure.Data;
using AgoneSentimentSales.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AgoneSentimentSales.Infrastructure.Services;

public class ReportsDataService : IReportsDataService
{
    private static readonly IReadOnlyList<ReportViewDefinition> Views =
    [
        new("tracker", "Live Tracker", "Real-time charts, progress & KPIs", "📊", "—"),
        new("dashboard", "Dashboard Summary", "Executive KPIs and sector breakdown", "📈", "LSE Dashboard Summary"),
        new("companies", "Company Profiles", "Top 100 LSE companies & offshoring status", "🏢", "LSE Company Profiles"),
        new("it-budgets", "IT Budget Breakdown", "Capex, Opex and cost categories", "💷", "LSE IT Budget Breakdown"),
        new("technology", "Technology Strategy", "Digital maturity, AI, cloud initiatives", "⚙️", "LSE Technology Strategy"),
        new("executives", "Executive Contacts", "CIO, CTO, CDO and IT leadership", "👤", "LSE Executive Contacts"),
        new("outsourcing", "Outsourcing Partners", "TCS, Infosys, Accenture and partners", "🤝", "LSE Outsourcing Partners"),
        new("leads", "Lead Generation", "Asia ops, hiring, announcements", "🎯", "LSE Lead Generation Data"),
        new("attribution", "Source Attribution", "Field-level provenance per source", "🔗", "Source Attribution"),
        new("source-summary", "Source Summary", "Counts by public data channel", "📋", "Source Summary Dashboard"),
        new("scraper-activity", "Scraper Activity", "When each source scraped & live feed", "🔄", "—"),
        new("scraper-config", "Scraper Configuration", "URLs, limits & enable/disable sources", "⚡", "—"),
        new("export", "Export & Reports", "Download Excel and job history", "📥", "Full Workbook")
    ];

    private readonly SentimentSalesDbContext _db;
    private readonly IMarketResearchService _research;

    public ReportsDataService(SentimentSalesDbContext db, IMarketResearchService research)
    {
        _db = db;
        _research = research;
    }

    public IReadOnlyList<ReportViewDefinition> GetViewDefinitions() => Views;

    public async Task<object> GetViewDataAsync(string viewId, Guid? jobId = null, CancellationToken cancellationToken = default)
    {
        return viewId switch
        {
            "dashboard" => await GetDashboardAsync(cancellationToken),
            "companies" => await GetCompaniesAsync(cancellationToken),
            "it-budgets" => await GetItBudgetsAsync(cancellationToken),
            "technology" => await GetTechnologyAsync(cancellationToken),
            "executives" => await GetExecutivesAsync(cancellationToken),
            "outsourcing" => await GetOutsourcingAsync(cancellationToken),
            "leads" => await GetLeadsAsync(cancellationToken),
            "attribution" => await GetAttributionAsync(jobId, cancellationToken),
            "source-summary" => await GetSourceSummaryAsync(jobId, cancellationToken),
            "scraper-activity" => await GetScraperActivityAsync(jobId, cancellationToken),
            _ => new { message = "Use client view for " + viewId }
        };
    }

    private async Task<object> GetDashboardAsync(CancellationToken ct)
    {
        var d = await _research.GetDashboardSummaryAsync(ct);
        return new DashboardSummaryDto(d.TotalCompanies, d.ConfirmedOffshoring, d.TotalEstimatedItBudgetGbpB,
            d.TotalOffshoreSpendGbpB, d.CompaniesWithIndiaOperations, d.CompaniesWithMultiplePartners,
            d.SectorBreakdowns.Select(s => new SectorBreakdownDto(s.Sector, s.CompanyCount, s.EstItBudgetGbpB, s.AvgItPercentRevenue)).ToList(),
            d.TopPartners.Select(p => new PartnerRankDto(p.Rank, p.Partner, p.CompanyCount)).ToList());
    }

    private async Task<object> GetCompaniesAsync(CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        return companies.Select(c => new CompanySummaryDto(c.Id, c.Rank, c.CompanyName, c.Ticker, c.Sector,
            c.MarketCapGbpB, c.OffshoringStatus.ToString(), c.PrimaryOffshoreLocations,
            c.LeadGeneration?.LeadScore ?? 0)).ToList();
    }

    private async Task<object> GetItBudgetsAsync(CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        return companies.Where(c => c.ItBudget != null).Select(c => new
        {
            c.CompanyName,
            c.Ticker,
            c.Sector,
            c.ItBudget!.FiscalYear,
            c.ItBudget.EstimatedItBudgetGbpM,
            c.ItBudget.CapexGbpM,
            c.ItBudget.OpexGbpM,
            c.ItBudget.OffshoreResourceCostGbpM,
            c.ItBudget.OnshoreResourceCostGbpM,
            c.ItBudget.CloudInfrastructureGbpM,
            c.ItBudget.ApplicationLicensingGbpM,
            c.ItBudget.ApplicationSupportGbpM,
            c.ItBudget.DataAndAiProjectsGbpM,
            c.ItBudget.EndUserComputingGbpM,
            c.ItBudget.CyberSecurityGbpM,
            c.ItBudget.ManagedServicesGbpM,
            c.ItBudget.OtherGbpM
        }).ToList();
    }

    private async Task<object> GetTechnologyAsync(CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        return companies.Where(c => c.TechnologyStrategy != null).Select(c => new
        {
            c.CompanyName,
            c.Ticker,
            Maturity = c.TechnologyStrategy!.DigitalMaturity.ToString(),
            c.TechnologyStrategy.AiMlPrograms,
            c.TechnologyStrategy.CloudStrategy,
            c.TechnologyStrategy.KeyTechInitiatives,
            c.TechnologyStrategy.AutomationFocus,
            c.TechnologyStrategy.DataAnalytics
        }).ToList();
    }

    private async Task<object> GetExecutivesAsync(CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        return companies.SelectMany(c => c.ExecutiveContacts.Select(e => new
        {
            c.CompanyName,
            c.Ticker,
            e.ExecutiveName,
            e.Title,
            RoleType = e.RoleType.ToString(),
            e.LinkedInUrl,
            e.EstimatedEmailFormat,
            e.Location,
            e.AreasOfResponsibility
        })).ToList();
    }

    private async Task<object> GetOutsourcingAsync(CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        return companies.Where(c => c.OutsourcingPartner != null).Select(c => new
        {
            c.CompanyName,
            c.OutsourcingPartner!.PrimaryPartners,
            c.OutsourcingPartner.SecondaryPartners,
            c.OutsourcingPartner.OffshoreDeliveryCenters,
            c.OutsourcingPartner.ContractType,
            c.OutsourcingPartner.EstimatedAnnualContractGbpM
        }).ToList();
    }

    private async Task<object> GetLeadsAsync(CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        return companies.Where(c => c.LeadGeneration != null).Select(c => new
        {
            c.CompanyName,
            c.LeadGeneration!.AsiaOperations,
            c.LeadGeneration.ItAnnouncements,
            c.LeadGeneration.HiringTrends,
            c.LeadGeneration.DigitalRoles,
            c.LeadGeneration.PainPoints,
            c.LeadGeneration.LeadScore
        }).ToList();
    }

    private async Task<object> GetAttributionAsync(Guid? jobId, CancellationToken ct)
    {
        var events = jobId.HasValue
            ? await _research.GetExtractionEventsAsync(jobId.Value, ct)
            : await _db.SourceExtractionEvents.AsNoTracking().OrderByDescending(e => e.ExtractedAt).Take(500).ToListAsync(ct);
        return events.Select(e => new ExtractionEventDto(e.Id, e.ResearchJobId, e.CompanyName, e.SourceType, e.SourceLabel,
            e.SourceUrl, e.FieldName, e.ExtractedValue, e.RawSnippet, e.ConfidenceScore, e.ExtractedAt)).ToList();
    }

    private async Task<object> GetSourceSummaryAsync(Guid? jobId, CancellationToken ct)
    {
        List<Domain.Entities.SourceExtractionEvent> events;
        if (jobId.HasValue)
            events = (await _research.GetExtractionEventsAsync(jobId.Value, ct)).ToList();
        else
            events = await _db.SourceExtractionEvents.AsNoTracking()
                .OrderByDescending(e => e.ExtractedAt).Take(5000).ToListAsync(ct);

        return events.GroupBy(e => e.SourceType).Select(g => new SourceSummaryDto(
            g.Key,
            g.Select(x => x.SourceLabel).FirstOrDefault() ?? g.Key,
            g.Count(),
            g.Average(x => x.ConfidenceScore))).OrderByDescending(s => s.FactCount).ToList();
    }

    private async Task<object> GetScraperActivityAsync(Guid? jobId, CancellationToken ct)
    {
        var q = _db.SourceExtractionEvents.AsNoTracking().AsQueryable();
        if (jobId.HasValue) q = q.Where(e => e.ResearchJobId == jobId);
        var events = await q.OrderByDescending(e => e.ExtractedAt).Take(200).ToListAsync(ct);
        var timeline = events.GroupBy(e => new { e.SourceType, Date = e.ExtractedAt.Date })
            .Select(g => new { g.Key.SourceType, g.Key.Date, Count = g.Count() })
            .OrderByDescending(x => x.Date).ToList();
        return new { timeline, recent = events.Take(50).Select(e => new ExtractionEventDto(e.Id, e.ResearchJobId, e.CompanyName, e.SourceType, e.SourceLabel,
            e.SourceUrl, e.FieldName, e.ExtractedValue, e.RawSnippet, e.ConfidenceScore, e.ExtractedAt)) };
    }
}

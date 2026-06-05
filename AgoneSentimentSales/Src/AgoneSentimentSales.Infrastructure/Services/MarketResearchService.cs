using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Enums;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Domain.Monitoring;
using AgoneSentimentSales.Shared.DTOs;
using AgoneSentimentSales.Infrastructure.Configuration;
using AgoneSentimentSales.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgoneSentimentSales.Infrastructure.Services;

public class MarketResearchService : IMarketResearchService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICompanyDataProvider _dataProvider;
    private readonly IResearchAgentService _agent;
    private readonly IScraperOrchestrator _scraperOrchestrator;
    private readonly IExcelExportService _excelExport;
    private readonly IResearchJobScheduler _scheduler;
    private readonly IJobTracker _jobTracker;
    private readonly ResearchSettings _settings;
    private readonly IResearchProgressPublisher _progress;
    private readonly ILogger<MarketResearchService> _logger;

    public MarketResearchService(
        IServiceScopeFactory scopeFactory,
        ICompanyDataProvider dataProvider,
        IResearchAgentService agent,
        IScraperOrchestrator scraperOrchestrator,
        IExcelExportService excelExport,
        IResearchJobScheduler scheduler,
        IJobTracker jobTracker,
        IResearchProgressPublisher progress,
        IOptions<ResearchSettings> settings,
        ILogger<MarketResearchService> logger)
    {
        _scopeFactory = scopeFactory;
        _dataProvider = dataProvider;
        _agent = agent;
        _scraperOrchestrator = scraperOrchestrator;
        _excelExport = excelExport;
        _scheduler = scheduler;
        _jobTracker = jobTracker;
        _progress = progress;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ResearchJob> StartResearchJobAsync(int companyCount = 100, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();

        if (await db.ResearchJobs.AnyAsync(j => j.Status == ResearchJobStatus.Running, cancellationToken))
            throw new InvalidOperationException("A research job is already running. Wait for it to complete before starting another.");

        var job = new ResearchJob { TargetCompanyCount = companyCount, Status = ResearchJobStatus.Pending };
        db.ResearchJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        await _scheduler.ScheduleResearchJobAsync(companyCount, job.Id, cancellationToken);
        job.Status = ResearchJobStatus.Running;
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task ExecuteResearchPipelineAsync(Guid jobId, int companyCount, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
        var job = await db.ResearchJobs.FindAsync([jobId], cancellationToken);
        if (job == null) return;

        using (_jobTracker.BeginScope(jobId))
        {
            try
            {
                job.Status = ResearchJobStatus.Running;
                await db.SaveChangesAsync(cancellationToken);
                await RunJobAsync(db, job, companyCount, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Research job {JobId} failed", jobId);
                job.Status = ResearchJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                await db.SaveChangesAsync(cancellationToken);
                await _progress.PublishProgressAsync(jobId, ResearchPhases.Failed, job.ProcessedCount, job.TargetCompanyCount,
                    message: ex.Message, cancellationToken: cancellationToken);
            }
        }
    }

    private async Task RunJobAsync(SentimentSalesDbContext db, ResearchJob job, int companyCount, CancellationToken cancellationToken)
    {
        await _progress.PublishProgressAsync(job.Id, ResearchPhases.Initializing, 0, companyCount,
            message: "Agentic research job started", cancellationToken: cancellationToken);

        var existing = await db.Companies.ToListAsync(cancellationToken);
        db.Companies.RemoveRange(existing);
        await db.SaveChangesAsync(cancellationToken);

        await _progress.PublishProgressAsync(job.Id, ResearchPhases.LoadingCompanies, 0, companyCount,
            message: "Loading top LSE companies by market cap", cancellationToken: cancellationToken);
        var seeds = await _dataProvider.GetTopCompaniesByMarketCapAsync(companyCount, cancellationToken);

        var enriched = new List<LseCompany>();
        var i = 0;
        foreach (var seed in seeds)
        {
            await _progress.PublishProgressAsync(job.Id, ResearchPhases.AgentEnrichment, i, seeds.Count,
                seed.CompanyName, "ICT analyst agent enriching IT budget, strategy, executives", cancellationToken);
            var company = await _agent.EnrichCompanyProfileAsync(seed, cancellationToken);
            db.Companies.Add(company);
            await db.SaveChangesAsync(cancellationToken);

            await _progress.PublishProgressAsync(job.Id, ResearchPhases.PublicSourceScraping, i, seeds.Count,
                company.CompanyName, "Scraping annual reports, LinkedIn, job boards, press, websites", cancellationToken);
            await _scraperOrchestrator.RunAllSourcesAsync(company, job.Id, cancellationToken);

            enriched.Add(company);
            i++;
            _jobTracker.ReportProgress(i, seeds.Count, company.CompanyName);
            job.ProcessedCount = i;
            await _progress.PublishProgressAsync(job.Id, ResearchPhases.Persisting, i, seeds.Count,
                company.CompanyName, cancellationToken: cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await _progress.PublishProgressAsync(job.Id, ResearchPhases.ExcelExport, seeds.Count, seeds.Count,
            message: "Building professional Excel workbook with dashboard and source attribution", cancellationToken: cancellationToken);
        var exportDir = Path.GetFullPath(_settings.ExportDirectory);
        Directory.CreateDirectory(exportDir);
        var events = await db.SourceExtractionEvents.Where(e => e.ResearchJobId == job.Id).ToListAsync(cancellationToken);
        var path = await _excelExport.SaveWorkbookAsync(enriched, events, exportDir, cancellationToken);
        job.OutputFilePath = path;
        job.Status = ResearchJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _progress.PublishProgressAsync(job.Id, ResearchPhases.Completed, seeds.Count, seeds.Count,
            message: path, cancellationToken: cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ResearchJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
        return await db.ResearchJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
    }


    public async Task<IReadOnlyList<ResearchJob>> GetRecentJobsAsync(int take = 20, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
        return await db.ResearchJobs.AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResearchJob?> GetLatestJobAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
        return await db.ResearchJobs.AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SourceExtractionEvent>> GetExtractionEventsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
        return await db.SourceExtractionEvents.AsNoTracking()
            .Where(e => e.ResearchJobId == jobId)
            .OrderByDescending(e => e.ExtractedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LseCompany>> GetCompaniesAsync(string? sector = null, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
        var q = db.Companies
            .Include(c => c.ItBudget).Include(c => c.TechnologyStrategy)
            .Include(c => c.ExecutiveContacts).Include(c => c.OutsourcingPartner).Include(c => c.LeadGeneration)
            .AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(sector)) q = q.Where(c => c.Sector == sector);
        return await q.OrderBy(c => c.Rank).ToListAsync(cancellationToken);
    }

    public async Task<LseCompany?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
        return await db.Companies.Include(c => c.ItBudget).Include(c => c.TechnologyStrategy)
            .Include(c => c.ExecutiveContacts).Include(c => c.OutsourcingPartner).Include(c => c.LeadGeneration)
            .AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var companies = await GetCompaniesAsync(cancellationToken: cancellationToken);
        if (companies.Count == 0)
            return new DashboardSummary(0, 0, 0, 0, 0, 0, [], []);

        var confirmed = companies.Count(c => c.OffshoringStatus == OffshoringStatus.Confirmed);
        var totalIt = companies.Where(c => c.ItBudget != null).Sum(c => c.ItBudget!.EstimatedItBudgetGbpM) / 1000m;
        var offshore = companies.Where(c => c.ItBudget != null).Sum(c => c.ItBudget!.OffshoreResourceCostGbpM) / 1000m;
        var indiaOps = companies.Count(c => (c.PrimaryOffshoreLocations ?? string.Empty).Contains("India", StringComparison.OrdinalIgnoreCase));
        var multiPartner = companies.Count(c => (c.OutsourcingPartner?.PrimaryPartners?.Split(',').Length ?? 0) > 2);

        var sectors = companies.GroupBy(c => c.Sector)
            .Select(g => new SectorBreakdown(g.Key, g.Count(),
                g.Where(x => x.ItBudget != null).Sum(x => x.ItBudget!.EstimatedItBudgetGbpM) / 1000m,
                g.Where(x => x.ItBudget != null).DefaultIfEmpty().Average(x => x?.ItBudget?.ItAsPercentOfRevenue ?? 0)))
            .OrderByDescending(s => s.EstItBudgetGbpB).ToList();

        var partnerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in companies.Where(c => c.OutsourcingPartner != null))
            foreach (var p in c.OutsourcingPartner!.PrimaryPartners.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                partnerCounts[p] = partnerCounts.GetValueOrDefault(p) + 1;

        var topPartners = partnerCounts.OrderByDescending(kv => kv.Value).Take(15)
            .Select((kv, idx) => new PartnerRank(idx + 1, kv.Key, kv.Value)).ToList();

        return new DashboardSummary(companies.Count, confirmed, totalIt, offshore, indiaOps, multiPartner, sectors, topPartners);
    }
}

using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Infrastructure.Data;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace AgoneSentimentSales.Infrastructure.Services;

public class ScraperConfigurationService : IScraperConfigurationService
{
    private readonly SentimentSalesDbContext _db;

    public ScraperConfigurationService(SentimentSalesDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScraperConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.ScraperConfigurations.AsNoTracking().OrderBy(c => c.Priority).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ScraperConfiguration>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
        await _db.ScraperConfigurations.AsNoTracking().Where(c => c.IsEnabled).OrderBy(c => c.Priority).ToListAsync(cancellationToken);

    public Task<ScraperConfiguration?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.ScraperConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<ScraperConfiguration?> GetBySourceTypeAsync(string sourceType, CancellationToken cancellationToken = default) =>
        _db.ScraperConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.SourceType == sourceType, cancellationToken);

    public async Task<ScraperConfiguration> CreateAsync(ScraperConfiguration config, CancellationToken cancellationToken = default)
    {
        config.CreatedAt = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;
        _db.ScraperConfigurations.Add(config);
        await _db.SaveChangesAsync(cancellationToken);
        return config;
    }

    public async Task<ScraperConfiguration?> UpdateAsync(ScraperConfiguration config, CancellationToken cancellationToken = default)
    {
        var existing = await _db.ScraperConfigurations.FindAsync([config.Id], cancellationToken);
        if (existing == null) return null;
        existing.DisplayName = config.DisplayName;
        existing.BaseUrlTemplate = config.BaseUrlTemplate;
        existing.IsEnabled = config.IsEnabled;
        existing.MaxItemsToScrape = config.MaxItemsToScrape;
        existing.DelayMsMin = config.DelayMsMin;
        existing.DelayMsMax = config.DelayMsMax;
        existing.Priority = config.Priority;
        existing.Notes = config.Notes;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.ScraperConfigurations.FindAsync([id], cancellationToken);
        if (existing == null) return false;
        _db.ScraperConfigurations.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task EnsureSeedDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _db.ScraperConfigurations.AnyAsync(cancellationToken)) return;
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            throw new InvalidOperationException(
                "Table sentimentsales.ScraperConfigurations is missing. Run: ./scripts/setup-database.sh or dotnet ef database update", ex);
        }

        var defaults = new[]
        {
            new ScraperConfiguration { SourceType = DataSourceTypes.AnnualReport, DisplayName = "Annual Reports", BaseUrlTemplate = "https://www.londonstockexchange.com/stock/{ticker}/", Priority = 1, MaxItemsToScrape = 15, Notes = "Investor relations & annual filings" },
            new ScraperConfiguration { SourceType = DataSourceTypes.LinkedIn, DisplayName = "LinkedIn", BaseUrlTemplate = "https://www.linkedin.com/company/{ticker}", Priority = 2, MaxItemsToScrape = 25, Notes = "Executive profiles & hiring signals" },
            new ScraperConfiguration { SourceType = DataSourceTypes.JobBoard, DisplayName = "Job Boards", BaseUrlTemplate = "https://www.glassdoor.co.uk/Search/results.htm?keyword={company}", Priority = 3, MaxItemsToScrape = 30, Notes = "IT & digital role postings" },
            new ScraperConfiguration { SourceType = DataSourceTypes.PressRelease, DisplayName = "Press Releases", BaseUrlTemplate = "https://www.google.com/search?q={company}+IT+transformation+press", Priority = 4, MaxItemsToScrape = 20, Notes = "News & transformation announcements" },
            new ScraperConfiguration { SourceType = DataSourceTypes.CompanyWebsite, DisplayName = "Company Website", BaseUrlTemplate = "https://www.{ticker}.com/careers", Priority = 5, MaxItemsToScrape = 12, Notes = "Corporate site & careers pages" },
            new ScraperConfiguration { SourceType = DataSourceTypes.InvestorRelations, DisplayName = "Investor Relations", BaseUrlTemplate = "https://www.{ticker}.com/investors", Priority = 6, MaxItemsToScrape = 10, IsEnabled = true },
            new ScraperConfiguration { SourceType = DataSourceTypes.Other, DisplayName = "Other Public Sources", BaseUrlTemplate = "https://find-and-update.company-information.service.gov.uk/search?q={company}", Priority = 7, MaxItemsToScrape = 8, IsEnabled = false }
        };

        _db.ScraperConfigurations.AddRange(defaults);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

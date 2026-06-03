using AgoneSentimentSales.Domain.Entities;

namespace AgoneSentimentSales.Domain.Interfaces;

public interface IScraperConfigurationService
{
    Task<IReadOnlyList<ScraperConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScraperConfiguration>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<ScraperConfiguration?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ScraperConfiguration?> GetBySourceTypeAsync(string sourceType, CancellationToken cancellationToken = default);
    Task<ScraperConfiguration> CreateAsync(ScraperConfiguration config, CancellationToken cancellationToken = default);
    Task<ScraperConfiguration?> UpdateAsync(ScraperConfiguration config, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task EnsureSeedDataAsync(CancellationToken cancellationToken = default);
}

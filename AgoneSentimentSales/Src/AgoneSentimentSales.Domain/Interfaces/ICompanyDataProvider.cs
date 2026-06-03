using AgoneSentimentSales.Domain.Entities;

namespace AgoneSentimentSales.Domain.Interfaces;

public interface ICompanyDataProvider
{
    Task<IReadOnlyList<LseCompany>> GetTopCompaniesByMarketCapAsync(int count, CancellationToken cancellationToken = default);
}

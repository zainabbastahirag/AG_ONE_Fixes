using AgoneSentimentSales.Core.Entities;

namespace AgoneSentimentSales.Core.Interfaces;

public interface ICompanyDataProvider
{
    Task<IReadOnlyList<LseCompany>> GetTopCompaniesByMarketCapAsync(int count, CancellationToken cancellationToken = default);
}

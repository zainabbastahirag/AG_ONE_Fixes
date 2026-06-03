using AgoneSentimentSales.Core.Entities;

namespace AgoneSentimentSales.Core.Interfaces;

public interface IResearchAgentService
{
    Task<LseCompany> EnrichCompanyProfileAsync(LseCompany company, CancellationToken cancellationToken = default);
}

using AgoneSentimentSales.Domain.Entities;

namespace AgoneSentimentSales.Domain.Interfaces;

public interface IResearchAgentService
{
    Task<LseCompany> EnrichCompanyProfileAsync(LseCompany company, CancellationToken cancellationToken = default);
}

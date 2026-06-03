using AgoneSentimentSales.Core.Entities;

namespace AgoneSentimentSales.Core.Interfaces;

public interface IMarketResearchService
{
    Task<ResearchJob> StartResearchJobAsync(int companyCount = 100, CancellationToken cancellationToken = default);
    Task<ResearchJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LseCompany>> GetCompaniesAsync(string? sector = null, CancellationToken cancellationToken = default);
    Task<LseCompany?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
}

public record DashboardSummary(
    int TotalCompanies,
    int ConfirmedOffshoring,
    decimal TotalEstimatedItBudgetGbpB,
    decimal TotalOffshoreSpendGbpB,
    int CompaniesWithIndiaOperations,
    int CompaniesWithMultiplePartners,
    IReadOnlyList<SectorBreakdown> SectorBreakdowns,
    IReadOnlyList<PartnerRank> TopPartners);

public record SectorBreakdown(string Sector, int CompanyCount, decimal EstItBudgetGbpB, decimal AvgItPercentRevenue);
public record PartnerRank(int Rank, string Partner, int CompanyCount);

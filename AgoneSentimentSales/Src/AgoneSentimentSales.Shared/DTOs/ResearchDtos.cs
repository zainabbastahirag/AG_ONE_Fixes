
namespace AgoneSentimentSales.Shared.DTOs;

public record StartResearchRequest(int CompanyCount = 100);
public record ResearchJobResponse(Guid JobId, string Status, int ProcessedCount, int TargetCount, string? OutputFilePath, string? ErrorMessage);

public record CompanySummaryDto(
    int Id, int Rank, string CompanyName, string Ticker, string Sector,
    decimal MarketCapGbpB, string OffshoringStatus, string PrimaryOffshoreLocations, int LeadScore);

public record CompanyDetailDto(
    CompanySummaryDto Summary,
    ItBudgetDto? Budget,
    TechnologyStrategyDto? Strategy,
    IReadOnlyList<ExecutiveContactDto> Executives,
    OutsourcingPartnerDto? Partners,
    LeadGenerationDto? LeadData);

public record ItBudgetDto(
    int FiscalYear, decimal AnnualRevenueGbpB, decimal EstimatedItBudgetGbpM,
    decimal ItAsPercentOfRevenue, decimal CapexGbpM, decimal OpexGbpM,
    decimal OffshoreResourceCostGbpM, decimal OnshoreResourceCostGbpM,
    decimal CloudInfrastructureGbpM, decimal ApplicationLicensingGbpM,
    decimal ApplicationSupportGbpM, decimal DataAndAiProjectsGbpM,
    decimal EndUserComputingGbpM, decimal CyberSecurityGbpM,
    decimal ManagedServicesGbpM, decimal OtherGbpM);

public record TechnologyStrategyDto(
    string DigitalMaturity, string AiMlPrograms, string CloudStrategy,
    string KeyTechInitiatives, string AutomationFocus, string DataAnalytics);

public record ExecutiveContactDto(
    string ExecutiveName, string Title, string RoleType,
    string LinkedInUrl, string EstimatedEmailFormat, string Location, string AreasOfResponsibility);

public record OutsourcingPartnerDto(
    string PrimaryPartners, string SecondaryPartners, string OffshoreDeliveryCenters,
    string ContractType, decimal? EstimatedAnnualContractGbpM, string PartnershipDuration);

public record LeadGenerationDto(
    string AsiaOperations, string ItAnnouncements, string HiringTrends,
    string DigitalRoles, string PainPoints, string RenewalCycle, int LeadScore);

public record DashboardSummaryDto(
    int TotalCompanies, int ConfirmedOffshoring, decimal TotalEstimatedItBudgetGbpB,
    decimal TotalOffshoreSpendGbpB, int CompaniesWithIndiaOperations,
    int CompaniesWithMultiplePartners, IReadOnlyList<SectorBreakdownDto> Sectors,
    IReadOnlyList<PartnerRankDto> TopPartners);

public record SectorBreakdownDto(string Sector, int CompanyCount, decimal EstItBudgetGbpB, decimal AvgItPercentRevenue);
public record PartnerRankDto(int Rank, string Partner, int CompanyCount);

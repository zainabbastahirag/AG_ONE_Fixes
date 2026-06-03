namespace AgoneSentimentSales.Domain.Entities;

public class ItBudgetBreakdown
{
    public int Id { get; set; }
    public int LseCompanyId { get; set; }
    public int FiscalYear { get; set; }
    public decimal AnnualRevenueGbpB { get; set; }
    public decimal EstimatedItBudgetGbpM { get; set; }
    public decimal ItAsPercentOfRevenue { get; set; }
    public decimal CapexGbpM { get; set; }
    public decimal OpexGbpM { get; set; }
    public decimal OffshoreResourceCostGbpM { get; set; }
    public decimal OnshoreResourceCostGbpM { get; set; }
    public decimal CloudInfrastructureGbpM { get; set; }
    public decimal ApplicationLicensingGbpM { get; set; }
    public decimal ApplicationSupportGbpM { get; set; }
    public decimal DataAndAiProjectsGbpM { get; set; }
    public decimal EndUserComputingGbpM { get; set; }
    public decimal CyberSecurityGbpM { get; set; }
    public decimal ManagedServicesGbpM { get; set; }
    public decimal OtherGbpM { get; set; }
    public string EstimationMethodology { get; set; } = string.Empty;

    public LseCompany? Company { get; set; }
}

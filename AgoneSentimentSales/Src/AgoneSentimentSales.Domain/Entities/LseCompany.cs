using AgoneSentimentSales.Domain.Enums;

namespace AgoneSentimentSales.Domain.Entities;

public class LseCompany
{
    public int Id { get; set; }
    public int Rank { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string IndustryGroup { get; set; } = string.Empty;
    public decimal MarketCapGbpB { get; set; }
    public string HqLocation { get; set; } = string.Empty;
    public OffshoringStatus OffshoringStatus { get; set; }
    public string PrimaryOffshoreLocations { get; set; } = string.Empty;
    public bool HasAsiaSubsidiary { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime? LastResearchedAt { get; set; }
    public string DataSourceNotes { get; set; } = string.Empty;

    public ItBudgetBreakdown? ItBudget { get; set; }
    public TechnologyStrategy? TechnologyStrategy { get; set; }
    public ICollection<ExecutiveContact> ExecutiveContacts { get; set; } = new List<ExecutiveContact>();
    public OutsourcingPartner? OutsourcingPartner { get; set; }
    public LeadGenerationData? LeadGeneration { get; set; }
}

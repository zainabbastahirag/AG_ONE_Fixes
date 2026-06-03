namespace AgoneSentimentSales.Core.Entities;

public class OutsourcingPartner
{
    public int Id { get; set; }
    public int LseCompanyId { get; set; }
    public string PrimaryPartners { get; set; } = string.Empty;
    public string SecondaryPartners { get; set; } = string.Empty;
    public string OffshoreDeliveryCenters { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;
    public decimal? EstimatedAnnualContractGbpM { get; set; }
    public string PartnershipDuration { get; set; } = string.Empty;

    public LseCompany? Company { get; set; }
}

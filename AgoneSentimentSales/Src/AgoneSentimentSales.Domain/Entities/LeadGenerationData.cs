namespace AgoneSentimentSales.Domain.Entities;

public class LeadGenerationData
{
    public int Id { get; set; }
    public int LseCompanyId { get; set; }
    public string AsiaOperations { get; set; } = string.Empty;
    public string ItAnnouncements { get; set; } = string.Empty;
    public string HiringTrends { get; set; } = string.Empty;
    public string DigitalRoles { get; set; } = string.Empty;
    public string PainPoints { get; set; } = string.Empty;
    public string RenewalCycle { get; set; } = string.Empty;
    public int LeadScore { get; set; }

    public LseCompany? Company { get; set; }
}

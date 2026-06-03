using AgoneSentimentSales.Core.Enums;

namespace AgoneSentimentSales.Core.Entities;

public class TechnologyStrategy
{
    public int Id { get; set; }
    public int LseCompanyId { get; set; }
    public DigitalMaturity DigitalMaturity { get; set; }
    public string AiMlPrograms { get; set; } = string.Empty;
    public string CloudStrategy { get; set; } = string.Empty;
    public string KeyTechInitiatives { get; set; } = string.Empty;
    public string AutomationFocus { get; set; } = string.Empty;
    public string DataAnalytics { get; set; } = string.Empty;
    public string DigitalTransformationEvidence { get; set; } = string.Empty;

    public LseCompany? Company { get; set; }
}

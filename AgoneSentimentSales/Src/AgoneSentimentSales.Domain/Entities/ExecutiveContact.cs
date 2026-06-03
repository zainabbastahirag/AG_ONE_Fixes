using AgoneSentimentSales.Domain.Enums;

namespace AgoneSentimentSales.Domain.Entities;

public class ExecutiveContact
{
    public int Id { get; set; }
    public int LseCompanyId { get; set; }
    public string ExecutiveName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ExecutiveRoleType RoleType { get; set; }
    public string LinkedInUrl { get; set; } = string.Empty;
    public string EstimatedEmailFormat { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string AreasOfResponsibility { get; set; } = string.Empty;
    public bool IsVerified { get; set; }

    public LseCompany? Company { get; set; }
}

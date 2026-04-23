namespace AGONECompliance.Shared.DTOs;

public class JobActivityDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string JobType { get; set; } = "";
    public string Step { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Message { get; set; }
    public string? ErrorDetail { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? DurationMs { get; set; }
}

public class ComplianceReportDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
    public int TotalChecks { get; set; }
    public int Compliant { get; set; }
    public int NonCompliant { get; set; }
    public int PartiallyCompliant { get; set; }
    public int Pending { get; set; }
    public double ComplianceRate { get; set; }
    public List<ComplianceCheckDto> Checks { get; set; } = new();
}

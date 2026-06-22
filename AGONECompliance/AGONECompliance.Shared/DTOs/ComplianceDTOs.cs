using AGONECompliance.Shared.Enums;

namespace AGONECompliance.Shared.DTOs;

public class CreateProjectRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class ProjectSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public int TotalChecks { get; set; }
    public int CompliantCount { get; set; }
    public int NonCompliantCount { get; set; }
    public int PendingCount { get; set; }
    public int DocumentCount { get; set; }
    public double ComplianceRate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ProjectDetailDto : ProjectSummaryDto
{
    public List<DocumentDto> Documents { get; set; } = new();
    public List<GuidelineRuleDto> Rules { get; set; } = new();
    public List<ComplianceCheckDto> Checks { get; set; } = new();
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = "";
    public string DocType { get; set; } = "";
    public string? BlobUrl { get; set; }
    public int? PageCount { get; set; }
    public string ExtractionStatus { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class GuidelineRuleDto
{
    public Guid Id { get; set; }
    public int RuleNumber { get; set; }
    public string Code { get; set; } = "";
    public string Paragraph { get; set; } = "";
    public string Requirement { get; set; } = "";
    public string Complexity { get; set; } = "";
    public string? Group { get; set; }
}

public class ComplianceCheckDto
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public int RuleNumber { get; set; }
    public string RuleCode { get; set; } = "";
    public string Paragraph { get; set; } = "";
    public string Requirement { get; set; } = "";
    public string Complexity { get; set; } = "";
    public string Result { get; set; } = "";
    public string? Finding { get; set; }
    public string? Evidence { get; set; }
    public string? PageReference { get; set; }
    public string? SectionReference { get; set; }
    public double ConfidenceScore { get; set; }
    public DateTime? CheckedAt { get; set; }
}

public class UploadDocumentRequest
{
    public string DocType { get; set; } = "";
}

public class RunComplianceRequest
{
    public Guid ProjectId { get; set; }
}

public class DashboardDto
{
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public int TotalChecks { get; set; }
    public int CompliantChecks { get; set; }
    public int NonCompliantChecks { get; set; }
    public double OverallComplianceRate { get; set; }
    public List<ProjectSummaryDto> RecentProjects { get; set; } = new();
}

public class ApiResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T data, string? message = null) => new() { Success = true, Data = data, Message = message };
    public static ApiResult<T> Fail(string message) => new() { Success = false, Message = message };
}

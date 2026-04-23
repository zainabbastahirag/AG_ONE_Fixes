using AGONECompliance.Shared.Enums;

namespace AGONECompliance.Shared.Models;

public class ComplianceProject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int TotalChecks { get; set; }
    public int CompliantCount { get; set; }
    public int NonCompliantCount { get; set; }
    public int PendingCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<ProjectDocument>? Documents { get; set; }
    public ICollection<ComplianceCheck>? Checks { get; set; }
}

public class ProjectDocument
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string FileName { get; set; } = "";
    public string BlobPath { get; set; } = "";
    public string? BlobUrl { get; set; }
    public DocumentType DocType { get; set; }
    public string? ExtractedText { get; set; }
    public int? PageCount { get; set; }
    public JobStatus ExtractionStatus { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public ComplianceProject? Project { get; set; }
}

public class GuidelineRule
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int RuleNumber { get; set; }
    public string Code { get; set; } = "";
    public string Paragraph { get; set; } = "";
    public string Requirement { get; set; } = "";
    public Complexity Complexity { get; set; }
    public string? Group { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public ComplianceProject? Project { get; set; }
    public ICollection<ComplianceCheck>? Checks { get; set; }
}

public class ComplianceCheck
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid RuleId { get; set; }
    public Guid? ProspectusDocId { get; set; }
    public CheckResult Result { get; set; } = CheckResult.Pending;
    public string? Finding { get; set; }
    public string? Evidence { get; set; }
    public string? PageReference { get; set; }
    public string? SectionReference { get; set; }
    public double ConfidenceScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ComplianceProject? Project { get; set; }
    public GuidelineRule? Rule { get; set; }
    public ProjectDocument? ProspectusDoc { get; set; }
}

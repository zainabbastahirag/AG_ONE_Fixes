using AGONECompliance.API.Data;
using AGONECompliance.API.Jobs;
using AGONECompliance.API.Services;
using AGONECompliance.Shared.DTOs;
using AGONECompliance.Shared.Enums;
using AGONECompliance.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace AGONECompliance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DocumentExtractionService _extraction;
    private readonly ISchedulerFactory _schedulerFactory;

    public ProjectsController(AppDbContext db, DocumentExtractionService extraction, ISchedulerFactory schedulerFactory)
    {
        _db = db;
        _extraction = extraction;
        _schedulerFactory = schedulerFactory;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResult<List<ProjectSummaryDto>>>> GetAll()
    {
        var projects = await _db.Projects
            .Include(p => p.Documents)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Status = p.Status.ToString(),
                TotalChecks = p.TotalChecks,
                CompliantCount = p.CompliantCount,
                NonCompliantCount = p.NonCompliantCount,
                PendingCount = p.PendingCount,
                DocumentCount = p.Documents!.Count,
                ComplianceRate = p.TotalChecks > 0 ? Math.Round((double)p.CompliantCount / p.TotalChecks * 100, 1) : 0,
                CreatedAt = p.CreatedAt,
                CompletedAt = p.CompletedAt
            })
            .ToListAsync();

        return Ok(ApiResult<List<ProjectSummaryDto>>.Ok(projects));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResult<ProjectDetailDto>>> Get(Guid id)
    {
        var p = await _db.Projects
            .Include(x => x.Documents)
            .Include(x => x.Checks)!.ThenInclude(c => c.Rule)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound(ApiResult<ProjectDetailDto>.Fail("Project not found"));

        var rules = await _db.Rules.Where(r => r.ProjectId == id).OrderBy(r => r.RuleNumber).ToListAsync();

        var dto = new ProjectDetailDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Status = p.Status.ToString(),
            TotalChecks = p.TotalChecks,
            CompliantCount = p.CompliantCount,
            NonCompliantCount = p.NonCompliantCount,
            PendingCount = p.PendingCount,
            DocumentCount = p.Documents?.Count ?? 0,
            ComplianceRate = p.TotalChecks > 0 ? Math.Round((double)p.CompliantCount / p.TotalChecks * 100, 1) : 0,
            CreatedAt = p.CreatedAt,
            CompletedAt = p.CompletedAt,
            Documents = p.Documents?.Select(d => new DocumentDto
            {
                Id = d.Id,
                FileName = d.FileName,
                DocType = d.DocType.ToString(),
                BlobUrl = d.BlobUrl,
                PageCount = d.PageCount,
                ExtractionStatus = d.ExtractionStatus.ToString(),
                CreatedAt = d.CreatedAt
            }).ToList() ?? new(),
            Rules = rules.Select(r => new GuidelineRuleDto
            {
                Id = r.Id,
                RuleNumber = r.RuleNumber,
                Code = r.Code,
                Paragraph = r.Paragraph,
                Requirement = r.Requirement,
                Complexity = r.Complexity.ToString(),
                Group = r.Group
            }).ToList(),
            Checks = p.Checks?.Where(c => c.Rule != null).Select(c => new ComplianceCheckDto
            {
                Id = c.Id,
                RuleId = c.RuleId,
                RuleNumber = c.Rule!.RuleNumber,
                RuleCode = c.Rule.Code,
                Paragraph = c.Rule.Paragraph,
                Requirement = c.Rule.Requirement,
                Complexity = c.Rule.Complexity.ToString(),
                Result = c.Result.ToString(),
                Finding = c.Finding,
                Evidence = c.Evidence,
                PageReference = c.PageReference,
                SectionReference = c.SectionReference,
                ConfidenceScore = c.ConfidenceScore,
                CheckedAt = c.CheckedAt
            }).OrderBy(c => c.RuleNumber).ToList() ?? new()
        };

        return Ok(ApiResult<ProjectDetailDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<ProjectSummaryDto>>> Create([FromBody] CreateProjectRequest req)
    {
        var project = new ComplianceProject
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            Status = JobStatus.Pending
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        return Ok(ApiResult<ProjectSummaryDto>.Ok(new ProjectSummaryDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status.ToString(),
            CreatedAt = project.CreatedAt
        }, "Project created"));
    }

    [HttpPost("{id}/documents")]
    public async Task<ActionResult<ApiResult<DocumentDto>>> UploadDocument(Guid id, IFormFile file, [FromQuery] string docType)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResult<DocumentDto>.Fail("Project not found"));

        if (!Enum.TryParse<DocumentType>(docType, true, out var dt))
            return BadRequest(ApiResult<DocumentDto>.Fail("Invalid document type. Use: Guide, Appendix, or Prospectus"));

        using var stream = file.OpenReadStream();
        var blobPath = await _extraction.UploadToBlobAsync(stream, file.FileName, id.ToString(), docType);

        var doc = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = id,
            FileName = file.FileName,
            BlobPath = blobPath,
            DocType = dt,
            ExtractionStatus = JobStatus.Pending
        };

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        return Ok(ApiResult<DocumentDto>.Ok(new DocumentDto
        {
            Id = doc.Id,
            FileName = doc.FileName,
            DocType = doc.DocType.ToString(),
            ExtractionStatus = doc.ExtractionStatus.ToString(),
            CreatedAt = doc.CreatedAt
        }, "Document uploaded"));
    }

    [HttpPost("{id}/run")]
    public async Task<ActionResult<ApiResult<string>>> RunChecks(Guid id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound(ApiResult<string>.Fail("Project not found"));

        var scheduler = await _schedulerFactory.GetScheduler();
        var job = JobBuilder.Create<ComplianceCheckJob>()
            .UsingJobData("ProjectId", id.ToString())
            .WithIdentity($"compliance-{id}")
            .Build();

        var trigger = TriggerBuilder.Create()
            .StartNow()
            .WithIdentity($"trigger-{id}")
            .Build();

        await scheduler.ScheduleJob(job, trigger);

        return Ok(ApiResult<string>.Ok("started", "Compliance check queued"));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResult<DashboardDto>>> Dashboard()
    {
        var projects = await _db.Projects.Include(p => p.Documents).ToListAsync();
        var checks = await _db.Checks.ToListAsync();

        var dto = new DashboardDto
        {
            TotalProjects = projects.Count,
            ActiveProjects = projects.Count(p => p.Status == JobStatus.Processing),
            TotalChecks = checks.Count,
            CompliantChecks = checks.Count(c => c.Result == CheckResult.Compliant),
            NonCompliantChecks = checks.Count(c => c.Result == CheckResult.NonCompliant),
            OverallComplianceRate = checks.Count > 0
                ? Math.Round((double)checks.Count(c => c.Result == CheckResult.Compliant) / checks.Count * 100, 1) : 0,
            RecentProjects = projects.OrderByDescending(p => p.CreatedAt).Take(5).Select(p => new ProjectSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                Status = p.Status.ToString(),
                TotalChecks = p.TotalChecks,
                CompliantCount = p.CompliantCount,
                NonCompliantCount = p.NonCompliantCount,
                ComplianceRate = p.TotalChecks > 0 ? Math.Round((double)p.CompliantCount / p.TotalChecks * 100, 1) : 0,
                CreatedAt = p.CreatedAt
            }).ToList()
        };

        return Ok(ApiResult<DashboardDto>.Ok(dto));
    }

    [HttpGet("{id}/report")]
    public async Task<ActionResult<ApiResult<ComplianceReportDto>>> GetReport(Guid id)
    {
        var p = await _db.Projects
            .Include(x => x.Checks)!.ThenInclude(c => c.Rule)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound(ApiResult<ComplianceReportDto>.Fail("Project not found"));

        var report = new ComplianceReportDto
        {
            ProjectId = p.Id,
            ProjectName = p.Name,
            CompletedAt = p.CompletedAt,
            TotalChecks = p.TotalChecks,
            Compliant = p.CompliantCount,
            NonCompliant = p.NonCompliantCount,
            PartiallyCompliant = p.Checks?.Count(c => c.Result == CheckResult.PartiallyCompliant) ?? 0,
            Pending = p.PendingCount,
            ComplianceRate = p.TotalChecks > 0 ? Math.Round((double)p.CompliantCount / p.TotalChecks * 100, 1) : 0,
            Checks = p.Checks?.Where(c => c.Rule != null).OrderBy(c => c.Rule!.RuleNumber).Select(c => new ComplianceCheckDto
            {
                Id = c.Id,
                RuleId = c.RuleId,
                RuleNumber = c.Rule!.RuleNumber,
                RuleCode = c.Rule.Code,
                Paragraph = c.Rule.Paragraph,
                Requirement = c.Rule.Requirement,
                Complexity = c.Rule.Complexity.ToString(),
                Result = c.Result.ToString(),
                Finding = c.Finding,
                Evidence = c.Evidence,
                PageReference = c.PageReference,
                SectionReference = c.SectionReference,
                ConfidenceScore = c.ConfidenceScore,
                CheckedAt = c.CheckedAt
            }).ToList() ?? new()
        };

        return Ok(ApiResult<ComplianceReportDto>.Ok(report));
    }

    [HttpGet("{id}/activities")]
    public async Task<ActionResult<ApiResult<List<JobActivityDto>>>> GetActivities(Guid id)
    {
        var activities = await _db.Activities
            .Where(a => a.ProjectId == id)
            .OrderByDescending(a => a.StartedAt)
            .Select(a => new JobActivityDto
            {
                Id = a.Id,
                ProjectId = a.ProjectId,
                JobType = a.JobType,
                Step = a.Step,
                Status = a.Status,
                Message = a.Message,
                ErrorDetail = a.ErrorDetail,
                StartedAt = a.StartedAt,
                CompletedAt = a.CompletedAt,
                DurationMs = a.DurationMs
            })
            .ToListAsync();

        return Ok(ApiResult<List<JobActivityDto>>.Ok(activities));
    }

    [HttpGet("activities/all")]
    public async Task<ActionResult<ApiResult<List<JobActivityDto>>>> GetAllActivities([FromQuery] int take = 50)
    {
        var activities = await _db.Activities
            .Join(_db.Projects, a => a.ProjectId, p => p.Id, (a, p) => new JobActivityDto
            {
                Id = a.Id,
                ProjectId = a.ProjectId,
                ProjectName = p.Name,
                JobType = a.JobType,
                Step = a.Step,
                Status = a.Status,
                Message = a.Message,
                ErrorDetail = a.ErrorDetail,
                StartedAt = a.StartedAt,
                CompletedAt = a.CompletedAt,
                DurationMs = a.DurationMs
            })
            .OrderByDescending(a => a.StartedAt)
            .Take(take)
            .ToListAsync();

        return Ok(ApiResult<List<JobActivityDto>>.Ok(activities));
    }
}

using AgoneSentimentSales.Domain.Enums;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgoneSentimentSales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrackerController : ControllerBase
{
    private static readonly string[] ExcelSheetNames =
    [
        "LSE Dashboard Summary",
        "LSE Company Profiles",
        "LSE IT Budget Breakdown",
        "LSE Technology Strategy",
        "LSE Executive Contacts",
        "LSE Outsourcing Partners",
        "LSE Lead Generation Data",
        "Source Attribution",
        "Source Summary Dashboard"
    ];

    private readonly IMarketResearchService _research;
    public TrackerController(IMarketResearchService research) => _research = research;

    [HttpGet("live")]
    public async Task<ActionResult<LiveTrackerDto>> GetLive([FromQuery] Guid? jobId, CancellationToken ct)
    {
        var d = await _research.GetDashboardSummaryAsync(ct);
        var dashboard = new DashboardSummaryDto(
            d.TotalCompanies, d.ConfirmedOffshoring, d.TotalEstimatedItBudgetGbpB,
            d.TotalOffshoreSpendGbpB, d.CompaniesWithIndiaOperations, d.CompaniesWithMultiplePartners,
            d.SectorBreakdowns.Select(s => new SectorBreakdownDto(s.Sector, s.CompanyCount, s.EstItBudgetGbpB, s.AvgItPercentRevenue)).ToList(),
            d.TopPartners.Select(p => new PartnerRankDto(p.Rank, p.Partner, p.CompanyCount)).ToList());

        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        var offshoring = companies
            .GroupBy(c => c.OffshoringStatus.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        ResearchJobResponse? jobDto = null;
        if (jobId.HasValue)
        {
            var job = await _research.GetJobAsync(jobId.Value, ct);
            if (job != null)
                jobDto = ToResponse(job);
        }
        else
        {
            var latest = await _research.GetLatestJobAsync(ct);
            if (latest != null)
                jobDto = ToResponse(latest);
        }

        var sourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (jobDto != null)
        {
            var events = await _research.GetExtractionEventsAsync(jobDto.JobId, ct);
            foreach (var g in events.GroupBy(e => e.SourceType))
                sourceCounts[g.Key] = g.Count();
        }

        return Ok(new LiveTrackerDto(dashboard, offshoring, sourceCounts, jobDto, ExcelSheetNames));
    }

    private static ResearchJobResponse ToResponse(Domain.Entities.ResearchJob job) =>
        new(job.Id, job.Status.ToString(), job.ProcessedCount, job.TargetCompanyCount, job.OutputFilePath, job.ErrorMessage);
}

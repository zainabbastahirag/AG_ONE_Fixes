using AgoneSentimentSales.Application.DTOs;
using AgoneSentimentSales.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AgoneSentimentSales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResearchController : ControllerBase
{
    private readonly IMarketResearchService _research;
    public ResearchController(IMarketResearchService research) => _research = research;

    [HttpPost("start")]
    public async Task<ActionResult<ResearchJobResponse>> Start([FromBody] StartResearchRequest request, CancellationToken ct)
    {
        var job = await _research.StartResearchJobAsync(request.CompanyCount, ct);
        return Ok(new ResearchJobResponse(job.Id, job.Status.ToString(), job.ProcessedCount, job.TargetCompanyCount, job.OutputFilePath, job.ErrorMessage));
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<ActionResult<ResearchJobResponse>> GetJob(Guid jobId, CancellationToken ct)
    {
        var job = await _research.GetJobAsync(jobId, ct);
        return job == null ? NotFound() : Ok(new ResearchJobResponse(job.Id, job.Status.ToString(), job.ProcessedCount, job.TargetCompanyCount, job.OutputFilePath, job.ErrorMessage));
    }

    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<CompanySummaryDto>>> GetCompanies([FromQuery] string? sector, CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(sector, ct);
        return Ok(companies.Select(c => new CompanySummaryDto(c.Id, c.Rank, c.CompanyName, c.Ticker, c.Sector, c.MarketCapGbpB, c.OffshoringStatus.ToString(), c.PrimaryOffshoreLocations, c.LeadGeneration?.LeadScore ?? 0)).ToList());
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSummaryDto>> Dashboard(CancellationToken ct)
    {
        var d = await _research.GetDashboardSummaryAsync(ct);
        return Ok(new DashboardSummaryDto(d.TotalCompanies, d.ConfirmedOffshoring, d.TotalEstimatedItBudgetGbpB, d.TotalOffshoreSpendGbpB, d.CompaniesWithIndiaOperations, d.CompaniesWithMultiplePartners,
            d.SectorBreakdowns.Select(s => new SectorBreakdownDto(s.Sector, s.CompanyCount, s.EstItBudgetGbpB, s.AvgItPercentRevenue)).ToList(),
            d.TopPartners.Select(p => new PartnerRankDto(p.Rank, p.Partner, p.CompanyCount)).ToList()));
    }
}

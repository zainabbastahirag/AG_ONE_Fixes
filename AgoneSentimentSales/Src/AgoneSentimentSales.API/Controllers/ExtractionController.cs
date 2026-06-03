using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.Constants;
using AgoneSentimentSales.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgoneSentimentSales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExtractionController : ControllerBase
{
    private readonly IMarketResearchService _research;

    public ExtractionController(IMarketResearchService research) => _research = research;

    [HttpGet("sources")]
    public ActionResult<IReadOnlyList<string>> GetSourceTypes() => Ok(DataSourceTypes.All);

    [HttpGet("jobs/{jobId:guid}/feed")]
    public async Task<ActionResult<LiveExtractionFeedDto>> GetFeed(Guid jobId, CancellationToken ct)
    {
        var job = await _research.GetJobAsync(jobId, ct);
        if (job == null) return NotFound();
        var events = await _research.GetExtractionEventsAsync(jobId, ct);
        var dtos = events.Select(e => new ExtractionEventDto(
            e.Id, e.ResearchJobId, e.CompanyName, e.SourceType, e.SourceLabel,
            e.SourceUrl, e.FieldName, e.ExtractedValue, e.RawSnippet, e.ConfidenceScore, e.ExtractedAt)).ToList();
        var bySource = dtos.GroupBy(e => e.SourceType).ToDictionary(g => g.Key, g => g.Count());
        return Ok(new LiveExtractionFeedDto(jobId, job.Status.ToString(), dtos.Count, dtos, bySource));
    }

    [HttpGet("jobs/{jobId:guid}/by-company")]
    public async Task<ActionResult<IReadOnlyList<CompanySourceBreakdownDto>>> ByCompany(Guid jobId, CancellationToken ct)
    {
        var events = await _research.GetExtractionEventsAsync(jobId, ct);
        var result = events.GroupBy(e => e.CompanyName).Select(g =>
        {
            var summaries = g.GroupBy(x => x.SourceType).Select(sg => new SourceSummaryDto(
                sg.Key, sg.First().SourceLabel, sg.Count(), sg.Average(x => x.ConfidenceScore))).ToList();
            var recent = g.OrderByDescending(x => x.ExtractedAt).Take(20).Select(e => new ExtractionEventDto(
                e.Id, e.ResearchJobId, e.CompanyName, e.SourceType, e.SourceLabel,
                e.SourceUrl, e.FieldName, e.ExtractedValue, e.RawSnippet, e.ConfidenceScore, e.ExtractedAt)).ToList();
            return new CompanySourceBreakdownDto(g.First().LseCompanyId ?? 0, g.Key, summaries, recent);
        }).ToList();
        return Ok(result);
    }
}

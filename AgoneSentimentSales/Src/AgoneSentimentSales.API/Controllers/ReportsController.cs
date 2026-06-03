using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AgoneSentimentSales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportsDataService _reports;

    public ReportsController(IReportsDataService reports) => _reports = reports;

    [HttpGet("views")]
    public ActionResult<IReadOnlyList<ReportViewMetaDto>> GetViews() =>
        Ok(_reports.GetViewDefinitions().Select(v => new ReportViewMetaDto(v.ViewId, v.Title, v.Description, v.Icon, v.ExcelSheetName)).ToList());

    [HttpGet("{viewId}")]
    public async Task<ActionResult<object>> GetViewData(string viewId, [FromQuery] Guid? jobId, CancellationToken ct) =>
        Ok(await _reports.GetViewDataAsync(viewId, jobId, ct));
}

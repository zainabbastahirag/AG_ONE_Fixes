using AgoneSentimentSales.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AgoneSentimentSales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly IMarketResearchService _research;
    private readonly IExcelExportService _excel;
    public ExportController(IMarketResearchService research, IExcelExportService excel) { _research = research; _excel = excel; }

    [HttpGet("excel")]
    public async Task<IActionResult> DownloadExcel(CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        if (companies.Count == 0) return BadRequest("No research data. Run POST /api/research/start first.");
        var bytes = await _excel.ExportWorkbookAsync(companies, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"LSE_TOP100_IT_OFFSHORING_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}

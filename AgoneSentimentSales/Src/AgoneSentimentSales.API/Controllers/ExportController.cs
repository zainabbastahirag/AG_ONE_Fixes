using AgoneSentimentSales.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgoneSentimentSales.Infrastructure.Data;

namespace AgoneSentimentSales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly IMarketResearchService _research;
    private readonly IExcelExportService _excel;
    private readonly SentimentSalesDbContext _db;

    public ExportController(IMarketResearchService research, IExcelExportService excel, SentimentSalesDbContext db)
    {
        _research = research;
        _excel = excel;
        _db = db;
    }

    [HttpGet("excel")]
    public async Task<IActionResult> DownloadExcel([FromQuery] Guid? jobId, CancellationToken ct)
    {
        var companies = await _research.GetCompaniesAsync(cancellationToken: ct);
        if (companies.Count == 0) return BadRequest("No research data. Run POST /api/research/start first.");
        var events = jobId.HasValue
            ? await _research.GetExtractionEventsAsync(jobId.Value, ct)
            : await _db.SourceExtractionEvents.AsNoTracking().ToListAsync(ct);
        var bytes = await _excel.ExportWorkbookAsync(companies, events, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"LSE_TOP100_IT_OFFSHORING_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}

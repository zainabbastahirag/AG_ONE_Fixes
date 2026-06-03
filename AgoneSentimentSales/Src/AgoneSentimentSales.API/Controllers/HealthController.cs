using AgoneSentimentSales.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgoneSentimentSales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private static readonly string[] RequiredTables =
    [
        "Companies", "ItBudgets", "TechnologyStrategies", "ExecutiveContacts",
        "OutsourcingPartners", "LeadGenerationData", "ResearchJobs",
        "SourceExtractionEvents", "SourcedDataPoints", "ScraperConfigurations", "ApiRequestLogs"
    ];

    private readonly SentimentSalesDbContext _db;

    public HealthController(SentimentSalesDbContext db) => _db = db;

    [HttpGet("database")]
    public async Task<ActionResult<object>> Database(CancellationToken ct)
    {
        var pending = (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();
        var applied = (await _db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        var missing = new List<string>();

        foreach (var table in RequiredTables)
        {
            var sql = $"SELECT 1 FROM [{SentimentSalesDbContext.SchemaName}].[{table}] WHERE 1=0";
            try
            {
                await _db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch
            {
                missing.Add($"{SentimentSalesDbContext.SchemaName}.{table}");
            }
        }

        var ok = pending.Count == 0 && missing.Count == 0;
        return Ok(new
        {
            status = ok ? "Healthy" : "ActionRequired",
            schema = SentimentSalesDbContext.SchemaName,
            pendingMigrations = pending,
            appliedMigrations = applied,
            missingTables = missing,
            fix = missing.Count > 0 || pending.Count > 0
                ? "Run: cd Src && dotnet ef database update --project AgoneSentimentSales.Infrastructure --startup-project AgoneSentimentSales.API"
                : null
        });
    }
}

using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Shared.Constants;
using Microsoft.AspNetCore.Mvc;

namespace AgoneSentimentSales.API.Controllers;

/// <summary>
/// Describes the CEO agentic research scope and pipeline for integrators.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    [HttpGet("scope")]
    public ActionResult<object> GetScope() => Ok(new
    {
        objective = "Expert ICT/Digital market analyst: profile top 100 LSE companies offshoring IT to India/Asia with IT budget, technology strategy, executive contacts, and lead generation data.",
        researchAreas = new[]
        {
            "Company identification by market cap and industry",
            "IT budget breakdown (Capex/Opex and 10+ cost categories)",
            "Technology strategy (Data & AI, digital transformation, automation)",
            "Executive contacts (CIO, CTO, CDO, infrastructure, cloud)",
            "Lead generation (Asia ops, outsourcing partners, announcements, hiring)"
        },
        publicSources = DataSourceTypes.All,
        outputs = new[] { "Real-time web dashboard", "Live source extraction monitor", "Professional multi-sheet Excel workbook" },
        architecture = new[] { "API", "UI", "Infrastructure", "Shared", "Domain" }
    });

    [HttpGet("pipeline")]
    public ActionResult<object> GetPipeline() => Ok(new
    {
        phases = new[]
        {
            new { phase = "Initializing", description = "Create research job, schedule Quartz worker" },
            new { phase = "LoadingCompanies", description = "Top 100 LSE by market cap from data provider" },
            new { phase = "AgentEnrichment", description = "ICT analyst agent: IT budget, strategy, executives, leads" },
            new { phase = "PublicSourceScraping", description = "Scraper bots: annual reports, LinkedIn, jobs, press, websites" },
            new { phase = "Persisting", description = "SQL Server sentimentsales schema + source attribution" },
            new { phase = "ExcelExport", description = "ClosedXML workbook with dashboard and 9 sheets" },
            new { phase = "Completed", description = "Job finished; Excel path returned" }
        },
        scheduling = new { onDemand = "POST /api/research/start", daily = "Quartz DailyRefreshJob 02:00 UTC" },
        realtime = new { progress = "/hubs/research-progress", extractions = "/hubs/extraction" }
    });
}

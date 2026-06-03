# AG ONE Sentiment Sales — Architecture

Aligned with **AGONEAIHub** (ONE Series) layered architecture.

## Project Layers

```
AgoneSentimentSales.API
  ├── Controllers/         Research, Export
  ├── Middleware/          ApiLoggingMiddleware, JobMonitoringMiddleware
  └── Extensions/          MonitoringExtensions (DI)

AgoneSentimentSales.Core            Zero-dependency kernel
  ├── Entities/            LseCompany, ItBudgetBreakdown, …
  ├── Interfaces/          IMarketResearchService, IExcelExportService, …
  ├── Enums/               OffshoringStatus, DigitalMaturity, …
  └── Monitoring/          IJobTracker

AgoneSentimentSales.Application     API DTOs
  └── DTOs/                ResearchDtos

AgoneSentimentSales.Infrastructure  External I/O
  ├── Configuration/       OpenAISettings, ResearchSettings
  ├── Data/                SentimentSalesDbContext
  └── Services/            MarketResearch, ExcelExport, ResearchAgent, …

AgoneSentimentSales.Shared          Shared helpers (future SDK)
CSS: `AgoneSentimentSales.API/wwwroot/css/agone.css`
*(UI served from API `wwwroot` — optional static HTML)*
```

## Middleware Pipeline

```
Request → CORS → ApiLoggingMiddleware → JobMonitoringMiddleware → Controllers
```

## External Services (Roadmap)

| Service | Config | Purpose |
|---------|--------|---------|
| Azure OpenAI | `OpenAI` | Agentic enrichment, summarisation |
| SQL Server | `ConnectionStrings:DefaultConnection` | Production persistence |
| LSE / market data API | TBD | Live market cap & listings |
| LinkedIn / web research | TBD | Contact verification (compliant) |

## Dependency Injection

| Type | Lifetime |
|------|----------|
| DbContext | Scoped |
| Research services | Scoped |
| IJobTracker | Singleton |

Database: **SQL Server** only, schema `sentimentsales`, EF migrations on startup.

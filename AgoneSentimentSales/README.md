# AG ONE Sentiment Sales

Market research and lead generation for **LSE top 100** IT offshoring intelligence — clean architecture (.NET 8), **SQL Server**, **Quartz** scheduling, public-source scrapers with **real-time attribution**.

## Solution layers (5 only)

| Project | Role |
|---------|------|
| `AgoneSentimentSales.API` | **Startup** — REST, SignalR, DI, static UI host |
| `AgoneSentimentSales.UI` | Presentation assets (Razor library) |
| `AgoneSentimentSales.Infrastructure` | EF Core, scrapers, Quartz, Excel |
| `AgoneSentimentSales.Shared` | DTOs, constants |
| `AgoneSentimentSales.Domain` | Entities, interfaces |

## Quick start

```bash
docker compose up -d sqlserver
# wait ~30s for SQL Server
cd Src
dotnet build
dotnet run --project AgoneSentimentSales.API --urls http://localhost:5080
```

- **Home:** http://localhost:5080  
- **Live scraper monitor:** http://localhost:5080/monitor.html  
- **Swagger:** http://localhost:5080/swagger  

## Public data sources

Annual reports, LinkedIn, job boards, press releases, and company websites — each extraction is tagged with **source type**, **URL**, and **field name**. View live on the monitor page or in Excel *Source Attribution* sheets.

## API highlights

| Endpoint | Purpose |
|----------|---------|
| `POST /api/research/start` | Start research + scrape job |
| `GET /api/extraction/jobs/{id}/feed` | Attribution feed by job |
| `GET /api/export/excel?jobId=` | Download workbook with source sheets |
| SignalR `/hubs/extraction` | Real-time `ExtractionReceived` events |

## SQL Server

Schema: **`sentimentsales`** (`Companies`, `SourceExtractionEvents`, `SourcedDataPoints`, …). Migrations apply on API startup.

## Documentation

| Doc | Description |
|-----|-------------|
| [ARCHITECTURE.md](Docs/ARCHITECTURE.md) | Layer reference |
| [CLEAN_ARCHITECTURE_FLOW.md](Docs/CLEAN_ARCHITECTURE_FLOW.md) | Mermaid flow diagrams |
| [CORE_ARCHITECTURE.md](Docs/CORE_ARCHITECTURE.md) | Detailed system design |
| [SYSTEM_FLOW.md](Docs/SYSTEM_FLOW.md) | Additional flows |
| [PRD.md](Docs/PRD.md) | Requirements |

## Folder layout

```
AgoneSentimentSales/
├── Src/          # .NET solution (5 projects)
├── Docs/         # PRD, architecture, diagrams
└── docker-compose.yml
```

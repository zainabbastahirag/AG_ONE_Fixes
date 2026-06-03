# AgoneSentimentSales — Clean Architecture

Five layers only. **One startup project:** `AgoneSentimentSales.API`.

## Layer map

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **API** | `AgoneSentimentSales.API` | HTTP controllers, SignalR hubs, DI composition, middleware, static host for UI assets |
| **UI** | `AgoneSentimentSales.UI` | Razor/static presentation assets (AG ONE styling); served via API `wwwroot` |
| **Infrastructure** | `AgoneSentimentSales.Infrastructure` | EF Core + SQL Server, scrapers, Quartz jobs, Excel export, external integrations |
| **Shared** | `AgoneSentimentSales.Shared` | DTOs, constants (`DataSourceTypes`), cross-layer contracts |
| **Domain** | `AgoneSentimentSales.Domain` | Entities, enums, interfaces (no framework dependencies) |

## Dependency rule

```
API → UI, Infrastructure, Shared, Domain
Infrastructure → Domain, Shared
UI → Shared
Shared → (none)
Domain → (none)
```

API must not reference Infrastructure implementation details in controllers except via interfaces registered in `ServiceRegistrationExtensions`.

## Data store

- **SQL Server**, schema **`sentimentsales`**
- EF Core migrations in `Infrastructure/Migrations`
- Quartz for background research pipeline and daily refresh (02:00 UTC cron)

## Public data sources

| Source type | Scraper | Typical fields |
|-------------|---------|----------------|
| AnnualReport | `AnnualReportScraper` | IT spend, capex/opex |
| LinkedIn | `LinkedInScraper` | Executives, hiring |
| JobBoard | `JobBoardScraper` | Digital roles, trends |
| PressRelease | `PressReleaseScraper` | IT announcements |
| CompanyWebsite | `CompanyWebsiteScraper` | Strategy, partners |

`ScraperOrchestrator` runs all sources per company; each fact is stored as `SourceExtractionEvent` with **source attribution**.

## Real-time & export

- **SignalR** hub `/hubs/extraction` — live feed on `/monitor.html`
- **Excel** — 9 sheets including *Source Attribution* and *Source Summary Dashboard*

See [CORE_ARCHITECTURE.md](CORE_ARCHITECTURE.md) and [SYSTEM_FLOW.md](SYSTEM_FLOW.md) for diagrams.

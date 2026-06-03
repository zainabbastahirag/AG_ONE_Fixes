# Clean Architecture — System Flow

## Layer diagram

```mermaid
flowchart TB
    subgraph UI["UI Layer"]
        WWW["wwwroot: index.html, monitor.html, agone.css"]
    end

    subgraph API["API Layer"]
        CTRL["Controllers: Research, Export, Extraction"]
        HUB["SignalR: ExtractionHub"]
        MW["Middleware: ApiLogging, JobMonitoring"]
        DI["ServiceRegistrationExtensions"]
    end

    subgraph INFRA["Infrastructure Layer"]
        EF["SentimentSalesDbContext — SQL Server"]
        SCR["Scrapers + ScraperOrchestrator"]
        QZ["Quartz: ResearchPipelineJob, DailyRefreshJob"]
        XLS["ExcelExportService"]
        AGT["ResearchAgentService"]
    end

    subgraph SHARED["Shared Layer"]
        DTO["DTOs + DataSourceTypes"]
    end

    subgraph DOMAIN["Domain Layer"]
        ENT["Entities, Enums, Interfaces"]
    end

    WWW --> CTRL
    WWW --> HUB
    CTRL --> DI
    DI --> INFRA
    HUB --> SCR
    CTRL --> ENT
    INFRA --> ENT
    INFRA --> DTO
    API --> DTO
```

## End-to-end research + scrape flow

```mermaid
sequenceDiagram
    participant U as User / monitor.html
    participant API as ResearchController
    participant Q as Quartz
    participant M as MarketResearchService
    participant S as ScraperOrchestrator
    participant DB as SQL Server
    participant SR as SignalR ExtractionHub

    U->>API: POST /api/research/start
    API->>M: StartResearchJobAsync
    M->>DB: Insert ResearchJob
    M->>Q: ScheduleResearchJobAsync(jobId)
    Q->>M: ExecuteResearchPipelineAsync
    loop Each LSE company
        M->>M: EnrichCompanyProfileAsync
        M->>S: RunAllSourcesAsync
        S->>DB: SourceExtractionEvents
        S->>SR: ExtractionReceived (per fact)
        SR-->>U: Live card with source label
    end
    M->>DB: Save companies + events
    M->>M: ExcelExport (with attribution sheets)
```

## Source attribution flow

```mermaid
flowchart LR
    AR[Annual Report] --> O[ScraperOrchestrator]
    LI[LinkedIn] --> O
    JB[Job Board] --> O
    PR[Press] --> O
    CW[Company Website] --> O
    O --> P[CompositeExtractionEventPublisher]
    P --> DB[(SourceExtractionEvents)]
    P --> SR[SignalR broadcast]
    DB --> XLS[Excel Source Attribution sheet]
    DB --> API[GET /api/extraction/jobs/id/feed]
```

## Quartz scheduling

| Job | Trigger | Action |
|-----|---------|--------|
| `ResearchPipelineJob` | On demand (per research start) | Full pipeline for one `jobId` |
| `DailyRefreshJob` | Cron `0 0 2 * * ?` | `StartResearchJobAsync(100)` |

## Excel workbook structure

1. LSE Dashboard Summary  
2. LSE Company Profiles  
3. LSE IT Budget Breakdown  
4. LSE Technology Strategy  
5. LSE Executive Contacts  
6. LSE Outsourcing Partners  
7. LSE Lead Generation Data  
8. **Source Attribution** (field-level provenance)  
9. **Source Summary Dashboard** (counts by channel)

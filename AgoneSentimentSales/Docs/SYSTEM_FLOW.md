# AG ONE Sentiment Sales — System Flows

End-to-end flows for how the system works and how it is implemented.

---

## Flow 1 — Application startup

```mermaid
sequenceDiagram
    autonumber
    participant OS as Operating System
    participant API as AgoneSentimentSales.API
    participant DI as DI Container
    participant EF as SentimentSalesDbContext
    participant SQL as SQL Server

    OS->>API: dotnet run (startup project)
    API->>DI: AddSentimentSalesServices()
    DI->>DI: Register DbContext, services, IJobTracker
    API->>EF: CreateScope → Database.Migrate()
    EF->>SQL: CREATE SCHEMA sentimentsales (if needed)
    EF->>SQL: Apply migrations → tables
    SQL-->>EF: sentimentsales.Companies, ItBudgets, …
    EF-->>API: Database ready
    API->>API: UseStaticFiles, MapControllers, Listen
```

**Implementation:** `Program.cs` lines — `AddSentimentSalesServices`, scoped `Migrate()`, then `MapControllers()`.

---

## Flow 2 — Start research job (main business flow)

This is the **primary agentic research pipeline**.

```mermaid
sequenceDiagram
    autonumber
    participant User as User / UI / Swagger
    participant RC as ResearchController
    participant MRS as MarketResearchService
    participant DB as SQL Server
    participant BG as Background Task
    participant LSE as LseSampleDataProvider
    participant Agent as ResearchAgentService
    participant XLS as ExcelExportService

    User->>RC: POST /api/research/start { companyCount: 100 }
    RC->>MRS: StartResearchJobAsync(100)
    MRS->>DB: INSERT sentimentsales.ResearchJobs (Status=Running)
    MRS-->>RC: ResearchJob { JobId, Status }
    RC-->>User: 200 OK + JobId

    Note over MRS,BG: Fire-and-forget background work
    MRS->>BG: Task.Run (new DI scope)

    BG->>DB: DELETE existing Companies (refresh run)
    BG->>LSE: GetTopCompaniesByMarketCapAsync(100)
    LSE-->>BG: List of seed LseCompany rows

    loop For each company (1..100)
        BG->>Agent: EnrichCompanyProfileAsync(company)
        Agent-->>BG: Enriched company + ItBudget + Strategy + Contacts + Lead
        BG->>DB: INSERT sentimentsales.Companies (+ related tables)
        BG->>DB: UPDATE ResearchJobs.ProcessedCount
    end

    BG->>XLS: SaveWorkbookAsync(companies, exports/)
    XLS-->>BG: file path (.xlsx)
    BG->>DB: UPDATE ResearchJobs (Completed, OutputFilePath)
```

### Step-by-step (implementation map)

| Step | What happens | Code location |
|------|----------------|---------------|
| 1 | HTTP POST received | `ResearchController.Start` |
| 2 | Job record created | `MarketResearchService.StartResearchJobAsync` |
| 3 | Response returned immediately | Job ID + `Running` status |
| 4 | New `IServiceScopeFactory` scope | Background `Task.Run` |
| 5 | Clear prior companies | `RunJobAsync` → `db.Companies.RemoveRange` |
| 6 | Load FTSE/LSE seed list | `LseSampleDataProvider` |
| 7 | Per-company enrichment | `ResearchAgentService.EnrichCompanyProfileAsync` |
| 8 | Persist relational data | EF Core → `sentimentsales.*` |
| 9 | Generate Excel | `ExcelExportService` → 7 sheets |
| 10 | Mark job complete | `ResearchJob.Status = Completed` |

---

## Flow 3 — Company enrichment (agent layer)

```mermaid
flowchart TD
    A[Seed LseCompany] --> B{Sector?}
    B -->|Banking| C[IT % ≈ 5.5% of revenue]
    B -->|Technology| D[IT % ≈ 8%]
    B -->|Other| E[IT % ≈ 3.5%]

    C --> F[Build ItBudgetBreakdown]
    D --> F
    E --> F

    F --> G[Set OffshoringStatus<br/>Confirmed / Partial / None]
    G --> H[TechnologyStrategy<br/>AI, Cloud, Automation]
    H --> I[ExecutiveContacts<br/>CIO, CDO]
    I --> J[OutsourcingPartner<br/>TCS, Infosys, …]
    J --> K[LeadGenerationData<br/>Score, hiring, pain points]
    K --> L[Return enriched LseCompany]

    style A fill:#EBF3FF
    style L fill:#C6EFCE
```

**MVP note:** `ResearchAgentService` uses deterministic heuristics keyed off ticker/sector. **Phase 2** replaces this with Azure OpenAI + external research sources via `IChatService`.

---

## Flow 4 — Poll job status

```mermaid
sequenceDiagram
    participant User
    participant RC as ResearchController
    participant MRS as MarketResearchService
    participant DB as sentimentsales.ResearchJobs

    User->>RC: GET /api/research/jobs/{jobId}
    RC->>MRS: GetJobAsync(jobId)
    MRS->>DB: SELECT by Id
    DB-->>MRS: Status, ProcessedCount, OutputFilePath
    MRS-->>RC: ResearchJob
    RC-->>User: JSON status
```

---

## Flow 5 — Dashboard aggregation

```mermaid
flowchart LR
    subgraph Input
        C[(sentimentsales.Companies)]
        B[(sentimentsales.ItBudgets)]
        O[(sentimentsales.OutsourcingPartners)]
    end

    subgraph MarketResearchService
        Q[Load all companies + includes]
        A1[Count Confirmed offshoring]
        A2[Sum IT budgets → £B]
        A3[Sum offshore spend]
        A4[Group by Sector]
        A5[Rank top partners]
    end

    subgraph Output
        D[DashboardSummaryDto]
    end

    C --> Q
    B --> Q
    O --> Q
    Q --> A1 & A2 & A3 & A4 & A5
    A1 & A2 & A3 & A4 & A5 --> D
```

**API:** `GET /api/research/dashboard` → `ResearchController.Dashboard`.

---

## Flow 6 — Excel export (on-demand download)

```mermaid
sequenceDiagram
    participant User
    participant EC as ExportController
    participant MRS as MarketResearchService
    participant XLS as ExcelExportService
    participant DB as SQL Server

    User->>EC: GET /api/export/excel
    EC->>MRS: GetCompaniesAsync()
    MRS->>DB: SELECT Companies + related entities
    DB-->>MRS: Full graph
    alt No data
        MRS-->>EC: Empty list
        EC-->>User: 400 Bad Request
    else Has data
        MRS-->>EC: Companies
        EC->>XLS: ExportWorkbookAsync()
        XLS-->>EC: byte[] .xlsx
        EC-->>User: File download
    end
```

**Note:** Research job also writes Excel to `exports/` folder during Flow 2.

---

## Flow 7 — Request logging

Every API call (after controller execution):

```
ApiLoggingMiddleware
  → INSERT sentimentsales.ApiRequestLogs
     (Method, Path, StatusCode, DurationMs, ClientIp)
```

Failures to save logs are non-blocking (swallowed).

---

## Flow 8 — Static UI (optional)

```mermaid
flowchart LR
    Browser[index.html + agone.css]
    Browser -->|POST /api/research/start| API
    Browser -->|GET /api/research/dashboard| API
    Browser -->|GET /api/export/excel| API
    Browser -->|/swagger| Swagger UI
```

**Files:** `wwwroot/index.html`, `wwwroot/css/agone.css`.

---

## Complete system context diagram

```mermaid
flowchart TB
    subgraph Clients
        UI[Browser / wwwroot]
        SW[Swagger]
        EXT[External systems - Phase 4]
    end

    subgraph API["AgoneSentimentSales.API"]
        CTRL[Controllers]
        MW[Middleware]
    end

    subgraph App["Application"]
        DTOs[DTOs]
    end

    subgraph Infra["Infrastructure"]
        MRS[MarketResearchService]
        AGENT[ResearchAgentService]
        PROV[LseSampleDataProvider]
        XLS[ExcelExportService]
        AI[OpenAIChatService - Phase 2]
    end

    subgraph Data
        SQL[(SQL Server<br/>schema: sentimentsales)]
        FILES[exports/*.xlsx]
    end

    subgraph Future["Phase 2+ sources"]
        LSEAPI[LSE Market API]
        AOAI[Azure OpenAI]
        WEB[Public web / LinkedIn]
    end

    UI & SW --> CTRL
    EXT -.-> CTRL
    CTRL --> DTOs
    CTRL --> MRS
    CTRL --> XLS
    MW --> SQL
    MRS --> AGENT & PROV
    MRS --> SQL
    MRS --> XLS
    XLS --> FILES
    AGENT -.-> AI
    PROV -.-> LSEAPI
    AGENT -.-> WEB
    AI -.-> AOAI
```

---

## Draw.io diagrams

For editable, presentation-ready diagrams open:

**[AgoneSentimentSales-Full-Flow.drawio](./diagrams/AgoneSentimentSales-Full-Flow.drawio)**

Pages included:

1. **Layered Architecture** — projects and dependencies  
2. **Research Job Flow** — full end-to-end sequence  
3. **Data & Database** — schema and relationships  
4. **API & Middleware** — request lifecycle  

Open at https://app.diagrams.net → File → Open from device.

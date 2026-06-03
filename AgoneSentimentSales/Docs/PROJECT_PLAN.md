# AG ONE Sentiment Sales — Project Plan

## Executive Summary
**AgoneSentimentSales** is an agentic market-research and lead-generation platform for the AG ONE marketplace programme. It profiles LSE-listed enterprises that offshore IT to India/Asia, enriches firmographics and technology signals, and exports sales-ready Excel workbooks styled like the KLSE reference templates (LSE/GBP).

## Solution Structure

```
AgoneSentimentSales/
├── Docs/                    PRD, architecture, diagrams, design reference
└── Src/
    ├── AgoneSentimentSales.sln
    ├── AgoneSentimentSales.API/
    ├── AgoneSentimentSales.Core/
    ├── AgoneSentimentSales.Application/
    ├── AgoneSentimentSales.Infrastructure/
    ├── AgoneSentimentSales.Shared/
    ├── AgoneSentimentSales.DesignSystem/
    └── AgoneSentimentSales.Web/
```

## Phase Roadmap (from PRD)

| Phase | Deliverables |
|-------|----------------|
| **Phase 1 (MVP)** ✅ | Layered .NET 8 solution, seed LSE top 100, heuristic agent enrichment, 7-sheet Excel export, Blazor dashboard |
| **Phase 2** | Azure OpenAI agent prompts, SQL Server, LSE/market API, contact enrichment, full AG ONE CSS bundle |
| **Phase 3** | Predictive lead scoring, sector analytics, scheduled refresh |
| **Phase 4** | CRM integration, real-time alerts, ONE Series SSO |

## Workstreams

### 1. Data Ingestion
- MVP: `LseSampleDataProvider` (FTSE 100 representative seed)
- Next: LSE RNS, annual reports (PDF via Document Intelligence), Companies House

### 2. AI / Agent Layer
- MVP: `ResearchAgentService` (rules + benchmarks)
- Next: `OpenAIChatService` + prompt templates (`sentimentsales_enrich_company`)

### 3. Processing & Normalisation
- EF Core schema `sentimentsales`
- Industry grouping, budget allocation model (2–8% revenue)

### 4. Analytics & Insights
- `GetDashboardSummaryAsync` — sector breakdown, partner ranks, offshore spend

### 5. Output Layer
- `ExcelExportService` (ClosedXML) — dark blue headers, conditional offshoring status, maturity highlighting

### 6. Frontend (Blazor WASM)
- AG ONE layout (sidebar, header, metrics cards)
- Pages: Home, Dashboard, Companies, Research Jobs, Excel download

## Milestones

| Milestone | Criteria |
|-----------|----------|
| Requirement sign-off | PRD approved |
| Prototype | API + Excel export for 15+ companies |
| Data validation | Sample spot-check vs public filings |
| Dashboard UAT | Blazor + Excel parity |
| Production | Azure App Service + SQL |

## Local Development

```bash
cd AgoneSentimentSales/Src
dotnet build
dotnet run --project AgoneSentimentSales.API --urls http://localhost:5080
dotnet run --project AgoneSentimentSales.Web
```

- API Swagger: `http://localhost:5080/swagger`
- Start research: `POST /api/research/start` with `{ "companyCount": 100 }`
- Download Excel: `GET /api/export/excel`

## Configuration (`appsettings.json`)

| Section | Purpose |
|---------|---------|
| `OpenAI` | Azure OpenAI endpoint & deployment |
| `AgoneSentimentSales` | Export path, default company count |
| `ConnectionStrings:DefaultConnection` | SQL Server (empty = InMemory) |

## Design System
- Package: `AgoneSentimentSales.DesignSystem` → `_content/.../css/agone.css`
- Full token reference: extend from AG ONE Figma/CSS (see user-provided framework in PRD attachment)

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Public data accuracy | Human-in-loop validation; confidence flags |
| Contact compliance | LinkedIn URLs only; no scraping behind login |
| Background job scope | `IServiceScopeFactory` per job |

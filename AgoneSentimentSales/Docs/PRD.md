# Product Requirements Document — AG ONE Sentiment Sales

## 1. Objective (CEO brief)

Act as an **expert ICT and Digital market analyst**. Conduct deep research and analysis to identify and profile the **top 100 London Stock Exchange listed companies** currently offshoring IT services to India or other Asian countries, with focus on:

- IT budget allocation  
- Technology strategy  
- Executive contacts for marketing lead generation  

Use **all available public domain sources**. Export to **Microsoft Excel** with professional-grade dashboard and formatting.

Deliver via an **agentic autonomous system** with **real-time dashboard** and **live source attribution**.

---

## 2. Research scope

### 2.1 Company identification

- Top 100 LSE-listed firms by market capitalization, grouped by industry  
- Confirm which actively offshore IT to India/Asia or other regions; specify countries when available  

### 2.2 IT budget breakdown

- Total IT budget (latest fiscal year)  
- Capex vs Opex  
- Categories: offshore/onshore resources, cloud (AWS/Azure/GCP), licensing, app support, data & AI, EUC, cyber security, managed services/outsourcing, other  

### 2.3 Technology strategy

- Data & AI adoption programmes  
- Digital transformation vs traditional IT  
- Automation, analytics, and AI initiatives  

### 2.4 Executive contacts

- LinkedIn or verified email patterns for: CIO/CTO, CDO, Head of IT Infrastructure, VP Applications/Cloud, other decision-makers  

### 2.5 Lead generation

- Subsidiary operations in Asia  
- Outsourcing partners (Infosys, TCS, Wipro, Accenture, etc.)  
- Recent IT transformation announcements  
- Hiring trends in IT & digital roles  

---

## 3. Output requirements

### 3.1 Excel workbook (9 sheets)

1. LSE Dashboard Summary  
2. LSE Company Profiles  
3. LSE IT Budget Breakdown  
4. LSE Technology Strategy  
5. LSE Executive Contacts  
6. LSE Outsourcing Partners  
7. LSE Lead Generation Data  
8. Source Attribution  
9. Source Summary Dashboard  

### 3.2 Real-time UI

- **CEO Dashboard** (`/dashboard.html`) — KPIs, sector table, agent progress  
- **Live Scraper Monitor** (`/monitor.html`) — per-source extraction feed  

---

## 4. Data sources (public domain)

| Source | Scraper |
|--------|---------|
| Annual reports / investor relations | `AnnualReportScraper` |
| LinkedIn (public profiles) | `LinkedInScraper` |
| Job boards | `JobBoardScraper` |
| Press releases | `PressReleaseScraper` |
| Company websites | `CompanyWebsiteScraper` |

---

## 5. Technical architecture

Five layers only:

| Layer | Project |
|-------|---------|
| API | `AgoneSentimentSales.API` (startup) |
| UI | `AgoneSentimentSales.UI` |
| Infrastructure | `AgoneSentimentSales.Infrastructure` |
| Shared | `AgoneSentimentSales.Shared` |
| Domain | `AgoneSentimentSales.Domain` |

- **Database:** SQL Server, schema `sentimentsales`  
- **Scheduling:** Quartz.NET (`ResearchPipelineJob`, `DailyRefreshJob`)  
- **Real-time:** SignalR (`/hubs/research-progress`, `/hubs/extraction`)  
- **Agent:** `ResearchAgentService` + optional Azure OpenAI (`OpenAIChatService`)  

See [DESIGN_DOCUMENT.md](DESIGN_DOCUMENT.md) for system and flow diagrams.

---

## 6. Success criteria

- [x] Autonomous job: start → research → scrape → Excel without manual steps  
- [x] CEO Excel categories covered  
- [x] Real-time progress and source attribution visible in browser  
- [x] Clean architecture with single startup project  
- [ ] Production scrapers with live HTTP (Phase 2)  
- [ ] Azure OpenAI enrichment (Phase 2)  

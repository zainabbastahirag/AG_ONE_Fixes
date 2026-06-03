# AG ONE Sentiment Sales

Market research and lead generation for **LSE top 100** IT offshoring intelligence — built on the AG ONE layered architecture (.NET 8 + Blazor WASM).

## Structure

| Folder | Contents |
|--------|----------|
| `Src/` | All application code |
| `Docs/` | PRD, project plan, architecture, Draw.io diagram |

## Quick start

```bash
cd Src
dotnet build
dotnet run --project AgoneSentimentSales.API --urls http://localhost:5080
# separate terminal
dotnet run --project AgoneSentimentSales.Web
```

## Documentation

- [Project Plan](Docs/PROJECT_PLAN.md)
- [PRD](Docs/PRD.md)
- [Architecture](Docs/ARCHITECTURE.md)
- [Draw.io Diagram](Docs/diagrams/AgoneSentimentSales-Architecture.drawio) — open at [diagrams.net](https://app.diagrams.net)

## Excel output sheets

1. LSE Dashboard Summary  
2. LSE Company Profiles  
3. LSE IT Budget Breakdown  
4. LSE Technology Strategy  
5. LSE Executive Contacts  
6. LSE Outsourcing Partners  
7. LSE Lead Generation Data  


## SQL Server (required)

```bash
docker compose up -d sqlserver
# wait ~30s, then:
cd Src
dotnet run --project AgoneSentimentSales.API
```

Tables are created under schema **sentimentsales** (e.g. `sentimentsales.Companies`, `sentimentsales.ItBudgets`).

**Startup project:** `AgoneSentimentSales.API` only.

## Architecture & flows

| Doc | Description |
|-----|-------------|
| [CORE_ARCHITECTURE.md](Docs/CORE_ARCHITECTURE.md) | How the system works |
| [SYSTEM_FLOW.md](Docs/SYSTEM_FLOW.md) | Full flow diagrams (Mermaid) |
| [AgoneSentimentSales-Full-Flow.drawio](Docs/diagrams/AgoneSentimentSales-Full-Flow.drawio) | Draw.io — open in [diagrams.net](https://app.diagrams.net) |

# LNK — AI-Powered LinkedIn Posts

LNK is a single-project ASP.NET Core 8 MVC SaaS application that generates professional LinkedIn posts daily using Ollama, emails them to users, and provides a premium review experience.

## Stack

- ASP.NET Core 8 MVC
- Entity Framework Core + SQL Server
- ASP.NET Identity
- Quartz.NET (scheduled generation)
- Ollama (local LLM)
- Bootstrap 5, HTMX, Alpine.js, Chart.js, Three.js
- Serilog, MailKit

## Quick start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server (local or Docker)
- [Ollama](https://ollama.com) with a model (e.g. `ollama pull llama3.2`)
- SMTP server (optional; use [Mailpit](https://github.com/axllent/mailpit) on port 1025 for dev)

### Run locally

```bash
cd LNK
dotnet restore
dotnet run
```

The app applies EF migrations and seeds demo data on startup.

Open http://localhost:5000 (or the URL shown in the console).

### Demo accounts

| Role  | Email           | Password        |
|-------|-----------------|-----------------|
| Admin | admin@lnk.app   | Lnk@Admin123!   |
| User  | demo@lnk.app    | Lnk@Demo123!    |

### Configuration

Edit `appsettings.json` or set environment variables:

| Key | Description |
|-----|-------------|
| `ConnectionStrings__DefaultConnection` | SQL Server connection |
| `Ollama__BaseUrl` | Ollama API URL (default `http://localhost:11434`) |
| `Ollama__Model` | Model name (default `llama3.2`) |
| `Email__Host` / `Email__Port` | SMTP settings |
| `Email__AppBaseUrl` | Public URL for review links in emails |

### Docker

```bash
docker build -t lnk-app .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal,1433;Database=LNK;..." \
  -e Ollama__BaseUrl="http://host.docker.internal:11434" \
  lnk-app
```

### SQL Server via Docker

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Your_password123" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

## Project structure

```
Controllers/   MVC controllers
Models/        EF entities
ViewModels/    View models
Services/      Ollama, email, post generation
Data/          DbContext, migrations, seeder
Jobs/          Quartz daily post job
EmailTemplates/ HTML email templates
Helpers/       Formatting utilities
Views/         Razor views
wwwroot/       CSS, JS, Three.js hero
Configuration/ Options classes
```

## Features

- Premium dark landing page with Three.js network hero
- User onboarding (industry, topics, tone, schedule)
- Dashboard with stats, Chart.js activity, quick generate
- Post review: edit, regenerate, copy, open LinkedIn
- Daily Quartz job: generate → save → email
- Admin area: users, posts, email logs, settings

## License

MIT — solo developer project.

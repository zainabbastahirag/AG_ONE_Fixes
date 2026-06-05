# LNK — AI-Powered LinkedIn Posts (Full Solution)

LNK is a **single-project** ASP.NET Core 8 MVC application. It generates professional LinkedIn posts with **Ollama**, emails them daily, and gives you a premium review page to copy content and open LinkedIn — no LinkedIn API required.

Open **`LNK.sln`** in Visual Studio 2022 or run from the command line.

---

## What you get

- Premium dark SaaS UI (Bootstrap 5, Alpine.js, HTMX, Chart.js, Three.js hero)
- ASP.NET Identity — register, login, remember me
- Onboarding — industry, topics, keywords, tone, length, schedule
- Dashboard — stats, Chart.js activity, quick generate, recent posts
- Review page — edit, regenerate, copy, open LinkedIn, copy & open + toast
- Quartz.NET — daily post generation + HTML email
- Admin — users, posts, email logs, system settings
- EF Core Code First — migrations applied automatically on startup

---

## Prerequisites (local only — no Docker)

| Tool | Purpose |
|------|---------|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | Build & run |
| **SQL Server** | Database — use one of the options below |
| [Ollama](https://ollama.com) | Local AI (`ollama pull llama3.2`) |
| SMTP (optional) | Emails — Gmail, SendGrid, or [Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP) for dev |

### SQL Server options (pick one)

**A) SQL Server Express / Developer (recommended)**  
Install from [Microsoft SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads).  
Use SSMS to create a database named `LNK`, or let the app create it on first run.

**B) LocalDB (Visual Studio)**  
Connection string:
```
Server=(localdb)\mssqllocaldb;Database=LNK;Trusted_Connection=True;TrustServerCertificate=True;
```

**C) Existing SQL Server instance**  
Update `ConnectionStrings:DefaultConnection` in `appsettings.json`.

---

## Quick start

### 1. Clone / open the project

```
LNK/
├── LNK.sln          ← open this in Visual Studio
├── LNK.csproj
├── Program.cs
├── appsettings.json
├── Controllers/
├── Models/
├── ViewModels/
├── Services/
├── Data/
├── Jobs/
├── EmailTemplates/
├── Helpers/
├── Views/
├── wwwroot/
└── Configuration/
```

### 2. Configure `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=LNK;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2"
  },
  "Email": {
    "Host": "localhost",
    "Port": 25,
    "FromAddress": "hello@lnk.app",
    "AppBaseUrl": "https://localhost:7254"
  }
}
```

Set `Email:AppBaseUrl` to the URL you use when running the app (see `Properties/launchSettings.json`).

For **Windows Authentication** to SQL Server, use `Trusted_Connection=True`.  
For **SQL login**, use: `Server=localhost;Database=LNK;User Id=sa;Password=YourPassword;TrustServerCertificate=True;`

### 3. Start Ollama

```bash
ollama pull llama3.2
ollama serve
```

### 4. Run the app

**Visual Studio:** Set `LNK` as startup project → F5 (HTTPS profile).

**Command line:**
```bash
cd LNK
dotnet restore
dotnet run --launch-profile https
```

Open: **https://localhost:7254** (or the URL shown in the console).

On first run the app will:
- Apply EF Core migrations
- Seed admin + demo users (if `App:SeedDemoData` is true)

---

## Demo accounts

| Role  | Email           | Password        |
|-------|-----------------|-----------------|
| Admin | admin@lnk.app   | Lnk@Admin123!   |
| User  | demo@lnk.app    | Lnk@Demo123!    |

---

## User flow

1. **Landing** — `/` — Three.js hero, pricing, FAQ  
2. **Register** — `/Account/Register`  
3. **Onboarding** — industry, topics, tone, daily time  
4. **Dashboard** — stats, quick generate, recent posts  
5. **Review** — `/Posts/Review/{id}` — copy & open LinkedIn  
6. **Daily job** — Quartz checks every 15 minutes; generates + emails when your scheduled time is due  

---

## Email (optional for testing)

Without SMTP, posts still generate — only email delivery fails (logged in `EmailLogs`).

**Dev on Windows:** Install [Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP) and set:
```json
"Email": { "Host": "localhost", "Port": 25, "UseSsl": false }
```

**Gmail:** Use an app password and set `Username` / `Password` in `Email` section (see `Configuration/EmailSettings.cs`).

---

## Admin

Sign in as `admin@lnk.app` → **Admin** in the nav:

- `/Admin/Users`
- `/Admin/Posts`
- `/Admin/EmailLogs`
- `/Admin/Settings`

---

## Database tables

| Table | Purpose |
|-------|---------|
| AspNetUsers | Identity users |
| UserSettings | Industry, topics, tone, schedule |
| Posts | Generated LinkedIn content |
| Schedules | Per-user Quartz schedule |
| EmailLogs | Send history |
| Settings | App-wide key/value config |

Migrations live in `Data/Migrations/`. To add a new migration:
```bash
dotnet ef migrations add YourMigrationName --project LNK.csproj
```

---

## Environment variables

Override any `appsettings.json` value:

```
ConnectionStrings__DefaultConnection=Server=...
Ollama__BaseUrl=http://localhost:11434
Ollama__Model=llama3.2
Email__Host=smtp.example.com
Email__AppBaseUrl=https://your-domain.com
```

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Cannot connect to SQL Server | Check instance name, enable TCP, use `TrustServerCertificate=True` |
| Ollama errors | Ensure `ollama serve` is running; app falls back to template content if Ollama is down |
| Emails not sending | Check `EmailLogs` table; configure SMTP host/port |
| Migrations fail | Delete DB and restart, or run `dotnet ef database update` |

Logs are written to `logs/lnk-*.log` (Serilog).

---

## License

MIT — solo developer project.

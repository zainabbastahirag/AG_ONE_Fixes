# 🎬 VidCV.live

**Turn your CV into a professional intro video — 100% free, no sign-up, no limits.**

Upload your CV (PDF) or enter your details manually → AI extracts key info → generates a stunning HD intro video you can download and share.

## Tech Stack

- **.NET 8** — Razor Pages (server-side rendered)
- **EF Core 8** — Code-first with SQL Server, auto-migration on startup
- **FFmpeg** — Video generation (slide-based MP4 with animated text)
- **PdfPig** — PDF text extraction (no paid APIs)
- **IIS** — Production deployment with .NET 8 Hosting Bundle

## Quick Start (Development)

```bash
# 1. Clone
git clone <repo-url>
cd VidCV/VidCV.Web

# 2. Default uses LocalDB — no config change needed if you have SQL Server LocalDB
#    Connection: Server=(localdb)\MSSQLLocalDB;Database=VidCV;Trusted_Connection=True

# 3. Run (DB auto-creates + seeds templates)
dotnet run
```

Open `http://localhost:5000`

## IIS Deployment (Production)

### Prerequisites on the Server

1. **Windows Server** with IIS enabled
2. **.NET 8 Hosting Bundle** — download from:
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0
   (look for "Hosting Bundle" under ASP.NET Core Runtime)
3. **SQL Server** (Express or higher)
4. **FFmpeg** — download from https://ffmpeg.org/download.html
   - Extract to `C:\ffmpeg`
   - Add `C:\ffmpeg\bin` to the system `PATH` environment variable
   - Restart IIS after adding to PATH

### Deployment Steps

```bash
# 1. Publish the app
dotnet publish -c Release -o ./publish

# 2. Copy the ./publish folder to your IIS server
```

3. **Create IIS Site:**
   - Open IIS Manager → Add Website
   - Site name: `VidCV`
   - Physical path: point to the `publish` folder
   - Binding: set port 80 (or your domain)
   - Application Pool: set to **No Managed Code** (.NET Core runs out-of-process)

4. **Update Connection String:**
   Edit `publish/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR_SERVER;Database=VidCV;Trusted_Connection=True;TrustServerCertificate=True"
     }
   }
   ```

5. **Set Folder Permissions:**
   - Give the IIS App Pool identity (`IIS AppPool\VidCV`) write access to:
     - `publish\wwwroot\uploads\`
     - `publish\wwwroot\videos\`
     - `publish\logs\`

6. **Create Required Folders:**
   ```cmd
   mkdir publish\wwwroot\uploads
   mkdir publish\wwwroot\videos
   mkdir publish\logs
   ```

7. **Start the site** — the database and seed data are auto-created on first request.

### Troubleshooting

- **500 errors?** Check `logs/stdout*.log` for details
- **FFmpeg not found?** Ensure `C:\ffmpeg\bin` is in system PATH and IIS was restarted
- **DB connection failed?** Verify SQL Server is running and the connection string is correct
- **Large file upload fails?** The `web.config` already allows up to 50MB

### LocalDB "Network-related error" Fix

If you get `Named Pipes Provider, error: 40` when using LocalDB:

**Step 1:** Make sure LocalDB is running:
```cmd
sqllocaldb start MSSQLLocalDB
sqllocaldb info MSSQLLocalDB
```
You should see `State: Running` and an `Instance pipe name`.

**Step 2:** The app has a **built-in auto-fix** — on startup it detects the LocalDB pipe name and retries the connection automatically. Just `dotnet run` and check the console for:
```
[VidCV] LocalDB pipe resolved: np:\\.\pipe\LOCALDB#xxxxx\tsql\query
```

**Step 3:** If auto-fix doesn't work, copy the pipe name from `sqllocaldb info` and use it directly:
```json
"DefaultConnection": "Data Source=np:\\\\.\\pipe\\LOCALDB#YOUR_ID\\tsql\\query;Initial Catalog=VidCV;Integrated Security=True;Encrypt=False"
```

**Step 4:** If nothing works, recreate the instance:
```cmd
sqllocaldb stop MSSQLLocalDB
sqllocaldb delete MSSQLLocalDB
sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

The app auto-creates the `VidCV` database on first run — you do NOT need to create it in SSMS.

For **IIS deployment** (production), switch to your full SQL Server:
```json
"Data Source=YOUR_SERVER;Initial Catalog=VidCV;Integrated Security=True;Encrypt=True;Trust Server Certificate=True"
```

## Project Structure

```
VidCV.Web/
├── Data/
│   ├── AppDbContext.cs          # EF Core context
│   ├── DbInitializer.cs        # Auto-migrate + seed
│   └── Migrations/             # EF Core migrations
├── Models/
│   ├── CvProfile.cs            # CV data model
│   └── VideoTemplate.cs        # Video style templates
├── Services/
│   ├── CvParserService.cs      # PDF → structured data
│   ├── ScriptGeneratorService.cs # Data → video script + slides
│   └── VideoGeneratorService.cs  # Slides → MP4 via FFmpeg
├── Pages/
│   ├── Index.cshtml             # Main single-page UI
│   ├── Index.cshtml.cs          # Page model
│   └── Shared/_Layout.cshtml    # Layout
├── wwwroot/
│   ├── css/site.css             # Full styling
│   ├── uploads/                 # Uploaded CVs
│   └── videos/                  # Generated videos
├── Program.cs                   # App startup + DI
├── appsettings.json             # Configuration
└── web.config                   # IIS configuration
```

## How It Works

1. **Upload CV** → PdfPig extracts text → regex + heuristics parse sections
2. **Generate Script** → AI-free script generator creates professional intro text
3. **Generate Slides** → Creates slide sequence: Intro → About → Experience → Skills → Education → Contact
4. **Render Video** → FFmpeg renders each slide as animated MP4 segments → concatenates into final HD video
5. **Download** → User gets the MP4 file directly

## License

Free and open source. Use it however you want.

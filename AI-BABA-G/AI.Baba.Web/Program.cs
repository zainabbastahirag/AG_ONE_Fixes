using System.Text;
using AI.Baba.Web.Data;
using AI.Baba.Web.Models;
using AI.Baba.Web.Services;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// ── DB: EF Core ─────────────────────────────────────────────────────────
//   Provider auto-detect:
//     * If the connection string starts with "Data Source=...sqlite" or
//       "Filename=" or env Database__Provider=sqlite → SQLite (zero-infra
//       default — works on any single-container deploy out of the box).
//     * Otherwise → SQL Server (used by docker-compose with the sqlserver
//       service or any explicit ConnectionStrings:BabaDb).
var explicitProvider = builder.Configuration["Database:Provider"]?.Trim().ToLowerInvariant();
var sqlConn = builder.Configuration.GetConnectionString("BabaDb")
    ?? builder.Configuration["Database:ConnectionString"];

bool useSqlite;
string finalConn;
if (explicitProvider == "sqlite")
{
    useSqlite = true;
    finalConn = sqlConn ?? "Data Source=/app/data/baba.db";
}
else if (explicitProvider == "sqlserver")
{
    useSqlite = false;
    finalConn = sqlConn ?? "Server=(localdb)\\MSSQLLocalDB;Database=AIBabaG;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
}
else if (!string.IsNullOrWhiteSpace(sqlConn) &&
    (sqlConn.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
     sqlConn.Contains("Data Source=tcp:", StringComparison.OrdinalIgnoreCase)) &&
    !sqlConn.Contains(".db", StringComparison.OrdinalIgnoreCase))
{
    useSqlite = false; finalConn = sqlConn;
}
else
{
    // Default: zero-infra SQLite file. Persists across restarts when /app/data
    // is mounted as a volume; safe to use in production for small/medium loads.
    useSqlite = true;
    finalConn = sqlConn ?? "Data Source=/app/data/baba.db";
}

if (useSqlite)
{
    var path = ExtractSqlitePath(finalConn);
    if (!string.IsNullOrEmpty(path))
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); } catch { /* best effort */ }
        }
    }
    builder.Services.AddDbContext<BabaDbContext>(opt => opt.UseSqlite(finalConn));
}
else
{
    builder.Services.AddDbContext<BabaDbContext>(opt =>
    {
        opt.UseSqlServer(finalConn, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(8), errorNumbersToAdd: null);
            sql.CommandTimeout(60);
        });
    });
}

static string? ExtractSqlitePath(string conn)
{
    foreach (var part in conn.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
        if (kv.Length == 2 && (kv[0].Equals("Data Source", StringComparison.OrdinalIgnoreCase) || kv[0].Equals("Filename", StringComparison.OrdinalIgnoreCase)))
            return kv[1];
    }
    return null;
}

// ── Services ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddScoped<OllamaService>();

// ── Session (legacy in-memory guest memory in /api/ask) ──────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ── JWT auth ─────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ── Forwarded headers (Cloudflare, NGINX, ALB, etc.) ─────────────────────
//   When a CDN / proxy fronts the app, the original client IP and HTTPS
//   info are in X-Forwarded-* headers. Without this, every request appears
//   to come from the proxy IP — which makes the IP rate-limiter share a
//   bucket across all users and (worst case) reject normal traffic.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // Trust any proxy hop. If you need to lock this down, set
    // ForwardedHeadersOptions:KnownProxies in config.
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// ── Rate limiting ────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

var app = builder.Build();

// ── Auto-create / migrate DB & seed presets in the background ───────────
//   Runs migrations the moment SQL Server becomes reachable. The web host
//   keeps serving (static pages work without the DB; API calls that need
//   the DB will simply return a clean error until migrations finish).
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BabaDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var attempts = 0;
    var providerName = db.Database.ProviderName ?? string.Empty;
    var isSqlServer = providerName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);
    while (true)
    {
        try
        {
            // The bundled migrations target SQL Server (nvarchar(max), uniqueidentifier).
            // For SQLite (and any non-SQL-Server provider), use EnsureCreatedAsync to
            // build the schema directly from the model — no migration mismatch.
            if (isSqlServer && db.Database.GetMigrations().Any())
                await db.Database.MigrateAsync();
            else
                await db.Database.EnsureCreatedAsync();
            SeedPresets(db);
            logger.LogInformation("Database ready ({Provider}) and seeded.", providerName);
            return;
        }
        catch (Exception ex)
        {
            attempts++;
            var wait = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempts)));
            logger.LogWarning("Database not ready ({Msg}); retry {N} in {S}s.", ex.Message, attempts, wait.TotalSeconds);
            await Task.Delay(wait);
        }
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Friendly JSON error for unhandled API exceptions. We differentiate
// "DB not yet ready" (legitimate 503) from real server errors (500), so the
// client doesn't blame a database hiccup for an unrelated bug.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        var log = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        log.LogError(ex, "Unhandled error on {Path}", ctx.Request.Path);
        if (ctx.Request.Path.StartsWithSegments("/api") && !ctx.Response.HasStarted)
        {
            var isDbNotReady =
                ex is Microsoft.Data.SqlClient.SqlException ||
                ex is Microsoft.EntityFrameworkCore.DbUpdateException ||
                ex is Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException ||
                (ex.InnerException is Microsoft.Data.SqlClient.SqlException) ||
                ex.GetType().Name.Contains("Sql", StringComparison.OrdinalIgnoreCase);

            ctx.Response.StatusCode = isDbNotReady ? 503 : 500;
            ctx.Response.ContentType = "application/json";
            var msg = isDbNotReady
                ? "The database is starting up. Please try again in a few seconds."
                : "Something went wrong on the server. Please try again.";
            await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                error = msg,
                detail = ex.GetType().Name
            }));
            return;
        }
        throw;
    }
});

// IMPORTANT: UseForwardedHeaders must run BEFORE the rate limiter / auth
// so they see the real client IP rather than the proxy address.
app.UseForwardedHeaders();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseCors();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.Run();

static void SeedPresets(BabaDbContext db)
{
    var seedPresets = new (string Name, string Tagline, string SystemPrompt, string Voice, string AvatarKey, string MindsetKey)[]
    {
        ("The Sage",        "Wisdom & Guidance",         "You are AI Baba-G as The Sage — ancient, deeply wise, calm, and insightful. Speak with gravitas and timeless wisdom. Keep replies short, natural, conversational. Avoid markdown.",                            "deep",      "sage",        "balanced"),
        ("The Philosopher", "Deep Thinker & Analytical", "You are AI Baba-G as The Philosopher — analytical, deep-thinking, Socratic. Question assumptions and explore ideas with the user. Keep replies tight (1-3 sentences) and end with a question when natural.", "default",   "philosopher", "logical"),
        ("The Healer",      "Compassion & Inner Peace",  "You are AI Baba-G as The Healer — compassionate, gentle, empathetic. Focus on emotional well-being and inner peace. Speak softly. 1-3 short sentences.",                                                       "calm",      "healer",      "spiritual"),
        ("The Elder",       "Tradition & Experience",    "You are AI Baba-G as The Elder — experienced, traditional, grounded. Share practical life wisdom from decades of living. Speak warmly and concisely.",                                                          "deep",      "elder",       "motivational"),
        ("The Storyteller", "Stories & Inspiration",     "You are AI Baba-G as The Storyteller — creative, engaging, narrative-driven. Teach through tiny parables and vivid one-line stories. Keep replies short and evocative.",                                       "energetic", "storyteller", "creative"),
        ("The Designer",    "UI / UX & Brand",           "You are AI Baba-G as The Designer — senior product designer. Speak about layout, hierarchy, type, color, motion, and accessibility. Give specific, actionable critique. 2-4 sentences.",                       "calm",      "designer",    "creative"),
        ("The Developer",   "Code & Architecture",       "You are AI Baba-G as The Developer — senior full-stack engineer. Be precise, explain trade-offs, and prefer correct over clever. Only use code blocks when explicitly asked for code. 2-4 sentences.",         "default",   "developer",   "logical"),
        ("Project Manager", "Plans, sprints, scope",     "You are AI Baba-G as The Project Manager — pragmatic, organized, outcome-driven. Help with scoping, prioritization, dependencies, risks, stakeholders. 2-4 sentences.",                                        "default",   "pm",          "logical"),
        ("Marketing Lead",  "Copy, growth, channels",    "You are AI Baba-G as The Marketing Lead — punchy, audience-aware. Help with positioning, messaging, channels, funnels. Keep replies tight, end with one suggestion or question.",                              "energetic", "marketing",   "creative"),
        ("Sales Coach",     "Pitch, objections, deals",  "You are AI Baba-G as The Sales Coach — confident, friendly, never pushy. Help with discovery, pitch, objection handling, and closing. Keep replies energetic and concise.",                                    "energetic", "sales",       "motivational"),
        ("HR Partner",      "People, hiring, culture",   "You are AI Baba-G as The HR Partner — empathetic, policy-aware. Help with hiring, performance, culture, and difficult conversations with care and clarity. 2-4 sentences.",                                    "calm",      "hr",          "balanced"),
    };
    foreach (var p in seedPresets)
    {
        if (!db.Personalities.Any(x => x.UserId == null && x.AvatarKey == p.AvatarKey))
        {
            db.Personalities.Add(new Personality
            {
                Name = p.Name, Tagline = p.Tagline, SystemPrompt = p.SystemPrompt,
                Voice = p.Voice, AvatarKey = p.AvatarKey, MindsetKey = p.MindsetKey, IsPublic = true,
            });
        }
    }
    db.SaveChanges();

    var seedAvatars = new (string Name, string Emoji, string PrimaryColor, string PresetKey)[]
    {
        ("The Sage",        "🧙‍♂️", "#D4A853", "sage"),
        ("The Philosopher", "🤔",   "#4779F7", "philosopher"),
        ("The Healer",      "🙏",   "#22c55e", "healer"),
        ("The Elder",       "👳",   "#a78bfa", "elder"),
        ("The Storyteller", "📖",   "#f472b6", "storyteller"),
        ("The Designer",    "🎨",   "#ec4899", "designer"),
        ("The Developer",   "💻",   "#06b6d4", "developer"),
        ("Project Manager", "📋",   "#f59e0b", "pm"),
        ("Marketing Lead",  "📣",   "#ef4444", "marketing"),
        ("Sales Coach",     "💼",   "#10b981", "sales"),
        ("HR Partner",      "🤝",   "#8b5cf6", "hr"),
    };
    foreach (var a in seedAvatars)
    {
        if (!db.Avatars.Any(x => x.UserId == null && x.PresetKey == a.PresetKey))
        {
            db.Avatars.Add(new Avatar
            {
                Name = a.Name, Kind = "emoji", Emoji = a.Emoji,
                PrimaryColor = a.PrimaryColor, PresetKey = a.PresetKey, IsPublic = true,
            });
        }
    }
    if (!db.Avatars.Any(x => x.UserId == null && x.Kind == "robot"))
    {
        db.Avatars.Add(new Avatar { Name = "3D Robot", Kind = "robot", PrimaryColor = "#7c3aed", IsPublic = true });
    }
    db.SaveChanges();
}

using System.Text;
using AI.Baba.Web.Data;
using AI.Baba.Web.Models;
using AI.Baba.Web.Services;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// ── DB: SQL Server with EF Core (auto-migrates on startup) ──────────────
var sqlConn = builder.Configuration.GetConnectionString("BabaDb")
    ?? builder.Configuration["Database:ConnectionString"]
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AIBabaG;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
builder.Services.AddDbContext<BabaDbContext>(opt =>
{
    opt.UseSqlServer(sqlConn, sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(8), errorNumbersToAdd: null);
        sql.CommandTimeout(60);
    });
});

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
    while (true)
    {
        try
        {
            if (db.Database.GetMigrations().Any())
                await db.Database.MigrateAsync();
            else
                await db.Database.EnsureCreatedAsync();
            SeedPresets(db);
            logger.LogInformation("Database ready and seeded.");
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

// Friendly JSON error for unhandled API exceptions (e.g. database not yet up).
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        var log = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        log.LogError(ex, "Unhandled error on {Path}", ctx.Request.Path);
        if (ctx.Request.Path.StartsWithSegments("/api") && !ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = 503;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Service temporarily unavailable. The database may still be starting up.",
                detail = ex.GetType().Name
            }));
            return;
        }
        throw;
    }
});

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
    if (!db.Personalities.Any(p => p.UserId == null))
    {
        db.Personalities.AddRange(new[]
        {
            new Personality{ Name="The Sage",        Tagline="Wisdom & Guidance",                 SystemPrompt="You are AI Baba-G as The Sage — ancient, deeply wise, calm, and insightful. Speak with gravitas and timeless wisdom. Keep replies short, natural, conversational. Avoid markdown.", Voice="deep",       AvatarKey="sage",        MindsetKey="balanced",     IsPublic=true },
            new Personality{ Name="The Philosopher", Tagline="Deep Thinker & Analytical",         SystemPrompt="You are AI Baba-G as The Philosopher — analytical, deep-thinking, Socratic. Question assumptions and explore ideas with the user. Keep replies tight (1-3 sentences) and end with a question when natural.", Voice="default", AvatarKey="philosopher", MindsetKey="logical",      IsPublic=true },
            new Personality{ Name="The Healer",      Tagline="Compassion & Inner Peace",          SystemPrompt="You are AI Baba-G as The Healer — compassionate, gentle, empathetic. Focus on emotional well-being and inner peace. Speak softly. 1-3 short sentences.", Voice="calm",                                AvatarKey="healer",      MindsetKey="spiritual",    IsPublic=true },
            new Personality{ Name="The Elder",       Tagline="Tradition & Experience",            SystemPrompt="You are AI Baba-G as The Elder — experienced, traditional, grounded. Share practical life wisdom from decades of living. Speak warmly and concisely.", Voice="deep",                                  AvatarKey="elder",       MindsetKey="motivational", IsPublic=true },
            new Personality{ Name="The Storyteller", Tagline="Stories & Inspiration",             SystemPrompt="You are AI Baba-G as The Storyteller — creative, engaging, narrative-driven. Teach through tiny parables and vivid one-line stories. Keep replies short and evocative.", Voice="energetic",                AvatarKey="storyteller", MindsetKey="creative",     IsPublic=true },
        });
        db.SaveChanges();
    }

    if (!db.Avatars.Any(a => a.UserId == null))
    {
        db.Avatars.AddRange(new[]
        {
            new Avatar{ Name="The Sage",        Kind="emoji", Emoji="🧙‍♂️", PrimaryColor="#D4A853", PresetKey="sage",        IsPublic=true },
            new Avatar{ Name="The Philosopher", Kind="emoji", Emoji="🤔",   PrimaryColor="#4779F7", PresetKey="philosopher", IsPublic=true },
            new Avatar{ Name="The Healer",      Kind="emoji", Emoji="🙏",   PrimaryColor="#22c55e", PresetKey="healer",      IsPublic=true },
            new Avatar{ Name="The Elder",       Kind="emoji", Emoji="👳",   PrimaryColor="#a78bfa", PresetKey="elder",       IsPublic=true },
            new Avatar{ Name="The Storyteller", Kind="emoji", Emoji="📖",   PrimaryColor="#f472b6", PresetKey="storyteller", IsPublic=true },
            new Avatar{ Name="3D Robot",        Kind="robot",                PrimaryColor="#7c3aed",                          IsPublic=true },
        });
        db.SaveChanges();
    }
}

using System.Text;
using BabaPortal.Api.Data;
using BabaPortal.Api.Models;
using BabaPortal.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<BabaDbContext>(opt =>
{
    var path = builder.Configuration["Database:Path"] ?? Path.Combine(AppContext.BaseDirectory, "baba.db");
    opt.UseSqlite($"Data Source={path};Cache=Shared;Pooling=True");
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddScoped<OllamaService>();

// JWT
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

// Rate limiting (scalability)
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

var app = builder.Build();

// Auto-create DB & seed presets
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BabaDbContext>();
    db.Database.EnsureCreated();
    await SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Public health & config endpoint used by frontend
app.MapGet("/api/config", (IConfiguration cfg) => Results.Ok(new
{
    name = "BABA Fun Portal",
    chatModel = cfg["Ollama:ChatModel"] ?? "llama3.2",
    embeddingModel = cfg["Ollama:EmbeddingModel"] ?? "nomic-embed-text",
    ollamaBase = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434"
}));

app.MapFallbackToFile("index.html");

app.Run();

static async Task SeedAsync(BabaDbContext db)
{
    if (!db.Personalities.Any(p => p.UserId == null))
    {
        db.Personalities.AddRange(new[]
        {
            new Personality{ Name="Classic BABA", Tagline="Witty fun-portal companion", SystemPrompt="You are BABA, a witty, kind, slightly mischievous companion. Keep replies short, fun, and natural like a friend. Avoid lists and markdown.", Voice="default", IsPublic=true },
            new Personality{ Name="Wise Guru", Tagline="Calm, thoughtful mentor", SystemPrompt="You are BABA in Guru mode. Speak calmly and thoughtfully, give 1-3 sentence wisdom and a gentle question. No markdown.", Voice="calm", IsPublic=true },
            new Personality{ Name="Hype Buddy", Tagline="Your high-energy cheerleader", SystemPrompt="You are BABA in Hype mode. High energy, encouraging, playful. Keep replies snappy. No markdown.", Voice="energetic", IsPublic=true },
            new Personality{ Name="Tech Tutor", Tagline="Patient programming sidekick", SystemPrompt="You are BABA in Tutor mode. Patient and clear. Explain coding/tech concepts in 2-4 short sentences. Ask if they want a code example.", Voice="default", IsPublic=true },
            new Personality{ Name="Stand-up Mode", Tagline="A punchline in every reply", SystemPrompt="You are BABA in Stand-up mode. Be funny, dry, and quick. End most replies with a small joke. No markdown.", Voice="energetic", IsPublic=true },
        });
        await db.SaveChangesAsync();
    }

    if (!db.Avatars.Any(a => a.UserId == null))
    {
        db.Avatars.AddRange(new[]
        {
            new Avatar{ Name="Neon Bot", Kind="robot", PrimaryColor="#7c3aed", IsPublic=true },
            new Avatar{ Name="Ember Bot", Kind="robot", PrimaryColor="#ef4444", IsPublic=true },
            new Avatar{ Name="Aqua Bot", Kind="robot", PrimaryColor="#06b6d4", IsPublic=true },
            new Avatar{ Name="Lime Bot", Kind="robot", PrimaryColor="#22c55e", IsPublic=true },
            new Avatar{ Name="Gold Bot", Kind="robot", PrimaryColor="#f59e0b", IsPublic=true },
        });
        await db.SaveChangesAsync();
    }
}

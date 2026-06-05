using AgoneSentimentSales.API.Extensions;
using AgoneSentimentSales.API.Hubs;
using AgoneSentimentSales.API.Middleware;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Infrastructure.Services;
using AgoneSentimentSales.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "AG ONE Sentiment Sales API", Version = "v1" }));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSignalR();
builder.Services.AddSentimentSalesServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
    var migrated = false;
    for (var attempt = 1; attempt <= 10 && !migrated; attempt++)
    {
        try
        {
            log.LogInformation("Applying migrations (attempt {Attempt})...", attempt);
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
                log.LogInformation("Pending migrations: {Migrations}", string.Join(", ", pending));
            await db.Database.MigrateAsync();
            var stuck = await db.ResearchJobs.Where(j => j.Status == AgoneSentimentSales.Domain.Enums.ResearchJobStatus.Running
                || j.Status == AgoneSentimentSales.Domain.Enums.ResearchJobStatus.Pending).ToListAsync();
            foreach (var j in stuck)
            {
                j.Status = AgoneSentimentSales.Domain.Enums.ResearchJobStatus.Failed;
                j.ErrorMessage = "Interrupted — reset on application startup.";
            }
            if (stuck.Count > 0)
            {
                await db.SaveChangesAsync();
                log.LogWarning("Reset {Count} stuck research job(s) to Failed", stuck.Count);
            }
            migrated = true;
            log.LogInformation("Database up to date. Applied: {Applied}",
                string.Join(", ", await db.Database.GetAppliedMigrationsAsync()));
        }
        catch (Exception ex) when (attempt < 10)
        {
            log.LogWarning(ex, "SQL Server not ready, retrying in 3s...");
            await Task.Delay(3000);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "SQL Server migration failed.");
            throw;
        }
    }

    var scraperConfig = scope.ServiceProvider.GetRequiredService<IScraperConfigurationService>();
    await scraperConfig.EnsureSeedDataAsync();

    var scheduler = scope.ServiceProvider.GetRequiredService<IResearchJobScheduler>();
    await scheduler.ScheduleDailyRefreshAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<ApiLoggingMiddleware>();
app.UseMiddleware<JobMonitoringMiddleware>();
app.MapControllers();
app.MapHub<ExtractionHub>("/hubs/extraction");
app.MapHub<ResearchProgressHub>("/hubs/research-progress");
app.MapFallbackToFile("index.html");
app.Run();

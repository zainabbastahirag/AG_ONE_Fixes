using AgoneSentimentSales.API.Extensions;
using AgoneSentimentSales.API.Hubs;
using AgoneSentimentSales.API.Middleware;
using AgoneSentimentSales.Domain.Interfaces;
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
            db.Database.Migrate();
            migrated = true;
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

using AgoneSentimentSales.API.Extensions;
using AgoneSentimentSales.API.Middleware;
using AgoneSentimentSales.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "AG ONE Sentiment Sales API", Version = "v1" }));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSentimentSalesServices(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SentimentSalesDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
    try
    {
        logger.LogInformation("Applying EF Core migrations to schema {Schema}...", SentimentSalesDbContext.SchemaName);
        db.Database.Migrate();
        logger.LogInformation("Database ready: sentimentsales.* tables");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "SQL Server migration failed. Ensure SQL Server is running and ConnectionStrings:DefaultConnection is valid.");
        throw;
    }
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
app.MapFallbackToFile("index.html");
app.Run();

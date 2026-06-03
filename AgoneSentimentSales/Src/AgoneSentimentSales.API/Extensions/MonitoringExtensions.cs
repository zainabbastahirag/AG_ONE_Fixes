using AgoneSentimentSales.Core.Interfaces;
using AgoneSentimentSales.Core.Monitoring;
using AgoneSentimentSales.Infrastructure.Configuration;
using AgoneSentimentSales.Infrastructure.Data;
using AgoneSentimentSales.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AgoneSentimentSales.API.Extensions;

public static class MonitoringExtensions
{
    public static IServiceCollection AddSentimentSalesServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAISettings>(configuration.GetSection(OpenAISettings.SectionName));
        services.Configure<ResearchSettings>(configuration.GetSection(ResearchSettings.SectionName));

        var conn = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for SQL Server.");

        services.AddDbContext<SentimentSalesDbContext>(o =>
            o.UseSqlServer(conn, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", SentimentSalesDbContext.SchemaName);
                sql.EnableRetryOnFailure(3);
            }));

        services.AddScoped<IMarketResearchService, MarketResearchService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<ICompanyDataProvider, LseSampleDataProvider>();
        services.AddScoped<IResearchAgentService, ResearchAgentService>();
        services.AddScoped<IChatService, OpenAIChatService>();
        services.AddSingleton<IJobTracker, JobTracker>();
        return services;
    }
}

using AgoneSentimentSales.API.Services;
using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Infrastructure.Configuration;
using AgoneSentimentSales.Infrastructure.Data;
using AgoneSentimentSales.Infrastructure.Jobs;
using AgoneSentimentSales.Infrastructure.Scrapers;
using AgoneSentimentSales.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace AgoneSentimentSales.API.Extensions;
public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddSentimentSalesServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAISettings>(configuration.GetSection(OpenAISettings.SectionName));
        services.AddOptions<ResearchSettings>()
            .Bind(configuration.GetSection(ResearchSettings.SectionName))
            .PostConfigure<IHostEnvironment>((opts, env) =>
            {
                if (!Path.IsPathRooted(opts.ExportDirectory))
                    opts.ExportDirectory = Path.Combine(env.ContentRootPath, opts.ExportDirectory);
            });
        var conn = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        services.AddDbContext<SentimentSalesDbContext>(o =>
            o.UseSqlServer(conn, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", SentimentSalesDbContext.SchemaName);
                sql.EnableRetryOnFailure(3);
            }));
        services.AddHttpClient();
        services.AddScoped<IMarketResearchService, MarketResearchService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<ICompanyDataProvider, LseSampleDataProvider>();
        services.AddScoped<IResearchAgentService, ResearchAgentService>();
        services.AddScoped<IChatService, OpenAIChatService>();
        services.AddScoped<IScraperOrchestrator, ScraperOrchestrator>();
        services.AddScoped<IResearchJobScheduler, QuartzResearchJobScheduler>();
        services.AddSingleton<Domain.Monitoring.IJobTracker, JobTracker>();
        services.AddScoped<IDataSourceScraper, AnnualReportScraper>();
        services.AddScoped<IDataSourceScraper, LinkedInScraper>();
        services.AddScoped<IDataSourceScraper, JobBoardScraper>();
        services.AddScoped<IDataSourceScraper, PressReleaseScraper>();
        services.AddScoped<IDataSourceScraper, CompanyWebsiteScraper>();
        services.AddScoped<DbExtractionEventPublisher>();
        services.AddScoped<SignalRExtractionEventPublisher>();
        services.AddScoped<IResearchProgressPublisher, SignalRResearchProgressPublisher>();
        services.AddScoped<IExtractionEventPublisher>(sp => new CompositeExtractionEventPublisher([
            sp.GetRequiredService<DbExtractionEventPublisher>(),
            sp.GetRequiredService<SignalRExtractionEventPublisher>()]));
        services.AddQuartz();
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AgoneSentimentSales.Infrastructure.Data;

public class SentimentSalesDbContextFactory : IDesignTimeDbContextFactory<SentimentSalesDbContext>
{
    public SentimentSalesDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "AgoneSentimentSales.API");
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var conn = config.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=AgoneSentimentSales;User Id=sa;Password=AgoneSentiment!2026;TrustServerCertificate=True;Encrypt=False;";

        var options = new DbContextOptionsBuilder<SentimentSalesDbContext>()
            .UseSqlServer(conn, b => b.MigrationsHistoryTable("__EFMigrationsHistory", SentimentSalesDbContext.SchemaName))
            .Options;

        return new SentimentSalesDbContext(options);
    }
}

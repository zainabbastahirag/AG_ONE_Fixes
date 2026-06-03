using AgoneSentimentSales.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgoneSentimentSales.Infrastructure.Data;

public class SentimentSalesDbContext : DbContext
{
    public SentimentSalesDbContext(DbContextOptions<SentimentSalesDbContext> options) : base(options) { }

    public DbSet<LseCompany> Companies => Set<LseCompany>();
    public DbSet<ItBudgetBreakdown> ItBudgets => Set<ItBudgetBreakdown>();
    public DbSet<TechnologyStrategy> TechnologyStrategies => Set<TechnologyStrategy>();
    public DbSet<ExecutiveContact> ExecutiveContacts => Set<ExecutiveContact>();
    public DbSet<OutsourcingPartner> OutsourcingPartners => Set<OutsourcingPartner>();
    public DbSet<LeadGenerationData> LeadGenerationData => Set<LeadGenerationData>();
    public DbSet<ResearchJob> ResearchJobs => Set<ResearchJob>();
    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sentimentsales");

        modelBuilder.Entity<LseCompany>(e =>
        {
            e.HasIndex(x => x.Ticker).IsUnique();
            e.HasIndex(x => x.Rank);
            e.Property(x => x.MarketCapGbpB).HasPrecision(18, 2);
        });

        modelBuilder.Entity<ItBudgetBreakdown>(e =>
        {
            e.HasOne(x => x.Company).WithOne(x => x.ItBudget).HasForeignKey<ItBudgetBreakdown>(x => x.LseCompanyId);
            e.Property(x => x.EstimatedItBudgetGbpM).HasPrecision(18, 2);
        });

        modelBuilder.Entity<TechnologyStrategy>(e =>
        {
            e.HasOne(x => x.Company).WithOne(x => x.TechnologyStrategy).HasForeignKey<TechnologyStrategy>(x => x.LseCompanyId);
        });

        modelBuilder.Entity<ExecutiveContact>(e =>
        {
            e.HasOne(x => x.Company).WithMany(x => x.ExecutiveContacts).HasForeignKey(x => x.LseCompanyId);
        });

        modelBuilder.Entity<OutsourcingPartner>(e =>
        {
            e.HasOne(x => x.Company).WithOne(x => x.OutsourcingPartner).HasForeignKey<OutsourcingPartner>(x => x.LseCompanyId);
        });

        modelBuilder.Entity<LeadGenerationData>(e =>
        {
            e.HasOne(x => x.Company).WithOne(x => x.LeadGeneration).HasForeignKey<LeadGenerationData>(x => x.LseCompanyId);
        });
    }
}

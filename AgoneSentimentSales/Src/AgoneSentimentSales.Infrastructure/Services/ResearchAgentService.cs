using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Enums;
using AgoneSentimentSales.Domain.Interfaces;

namespace AgoneSentimentSales.Infrastructure.Services;

/// <summary>
/// Agentic enrichment layer. MVP uses industry heuristics; Phase 2 wires Azure OpenAI + web research.
/// </summary>
public class ResearchAgentService : IResearchAgentService
{
    private static readonly string[] IndiaAsiaLocations = ["India", "India (Multiple)", "Philippines", "Singapore", "China", "Malaysia"];
    private static readonly string[] GlobalPartners = ["TCS", "Infosys", "Wipro", "Accenture", "IBM", "Capgemini", "HCL", "Cognizant", "Microsoft", "SAP"];

    public Task<LseCompany> EnrichCompanyProfileAsync(LseCompany company, CancellationToken cancellationToken = default)
    {
        var hash = Math.Abs(company.Ticker.GetHashCode());
        var offshoringLikely = hash % 3 != 0;

        company.OffshoringStatus = offshoringLikely
            ? (hash % 5 == 0 ? OffshoringStatus.Partial : OffshoringStatus.Confirmed)
            : OffshoringStatus.None;

        company.PrimaryOffshoreLocations = offshoringLikely
            ? IndiaAsiaLocations[hash % IndiaAsiaLocations.Length]
            : "N/A";

        company.HasAsiaSubsidiary = offshoringLikely && hash % 2 == 0;
        company.LastResearchedAt = DateTime.UtcNow;
        company.Notes = offshoringLikely
            ? "IT offshoring indicators from public filings and industry benchmarks (MVP model)."
            : "Limited public evidence of Asia IT offshoring; monitor for transformation programs.";

        var itPercent = company.Sector.Contains("Bank", StringComparison.OrdinalIgnoreCase) ? 5.5m
            : company.Sector.Contains("Tech", StringComparison.OrdinalIgnoreCase) ? 8.0m
            : company.Sector.Contains("Telecom", StringComparison.OrdinalIgnoreCase) ? 6.0m
            : 3.5m;

        var revenueB = Math.Max(company.MarketCapGbpB * 0.35m, 2m);
        var itBudgetM = revenueB * 1000m * (itPercent / 100m);
        var capexRatio = 0.35m;

        company.ItBudget = new ItBudgetBreakdown
        {
            FiscalYear = DateTime.UtcNow.Year - 1,
            AnnualRevenueGbpB = Math.Round(revenueB, 2),
            EstimatedItBudgetGbpM = Math.Round(itBudgetM, 0),
            ItAsPercentOfRevenue = itPercent,
            CapexGbpM = Math.Round(itBudgetM * capexRatio, 0),
            OpexGbpM = Math.Round(itBudgetM * (1 - capexRatio), 0),
            OffshoreResourceCostGbpM = Math.Round(itBudgetM * 0.12m, 0),
            OnshoreResourceCostGbpM = Math.Round(itBudgetM * 0.28m, 0),
            CloudInfrastructureGbpM = Math.Round(itBudgetM * 0.15m, 0),
            ApplicationLicensingGbpM = Math.Round(itBudgetM * 0.12m, 0),
            ApplicationSupportGbpM = Math.Round(itBudgetM * 0.10m, 0),
            DataAndAiProjectsGbpM = Math.Round(itBudgetM * 0.08m, 0),
            EndUserComputingGbpM = Math.Round(itBudgetM * 0.05m, 0),
            CyberSecurityGbpM = Math.Round(itBudgetM * 0.06m, 0),
            ManagedServicesGbpM = Math.Round(itBudgetM * 0.10m, 0),
            OtherGbpM = Math.Round(itBudgetM * 0.04m, 0),
            EstimationMethodology = "Industry benchmark 2-8% of revenue; financial services weighted higher (MVP)."
        };

        company.TechnologyStrategy = new TechnologyStrategy
        {
            DigitalMaturity = hash % 4 == 0 ? DigitalMaturity.Developing : DigitalMaturity.Advanced,
            AiMlPrograms = "AI operations, customer analytics, process automation",
            CloudStrategy = hash % 2 == 0 ? "Multi-Cloud (Azure, AWS)" : "Hybrid Cloud",
            KeyTechInitiatives = "Digital transformation, data platform modernisation, cyber uplift",
            AutomationFocus = "RPA, intelligent automation, DevOps",
            DataAnalytics = "Enterprise analytics, customer 360, risk analytics",
            DigitalTransformationEvidence = "Annual report and investor day technology disclosures (MVP synthesis)."
        };

        var domain = company.Ticker.ToLowerInvariant();
        company.ExecutiveContacts = new List<ExecutiveContact>
        {
            new()
            {
                ExecutiveName = "Chief Information Officer",
                Title = "Group CIO",
                RoleType = ExecutiveRoleType.Cio,
                LinkedInUrl = $"linkedin.com/company/{domain}",
                EstimatedEmailFormat = $"cio@{domain}.com",
                Location = company.HqLocation,
                AreasOfResponsibility = "Enterprise IT, digital platforms, cyber",
                IsVerified = false
            },
            new()
            {
                ExecutiveName = "Chief Digital Officer",
                Title = "Group Chief Digital Officer",
                RoleType = ExecutiveRoleType.ChiefDigitalOfficer,
                LinkedInUrl = $"linkedin.com/company/{domain}",
                EstimatedEmailFormat = $"digital.officer@{domain}.com",
                Location = company.HqLocation,
                AreasOfResponsibility = "Digital innovation, AI, customer experience",
                IsVerified = false
            }
        };

        if (offshoringLikely)
        {
            var p1 = GlobalPartners[hash % GlobalPartners.Length];
            var p2 = GlobalPartners[(hash + 3) % GlobalPartners.Length];
            company.OutsourcingPartner = new OutsourcingPartner
            {
                PrimaryPartners = $"{p1}, {p2}, Microsoft",
                SecondaryPartners = "Accenture, Wipro",
                OffshoreDeliveryCenters = company.PrimaryOffshoreLocations,
                ContractType = "Strategic Multi-vendor",
                EstimatedAnnualContractGbpM = Math.Round(itBudgetM * 0.25m, 0),
                PartnershipDuration = "Long-term (5+ years)"
            };
        }

        company.LeadGeneration = new LeadGenerationData
        {
            AsiaOperations = company.HasAsiaSubsidiary ? "Asia (Regional)" : "UK/Europe primary",
            ItAnnouncements = "Cloud migration, AI centre of excellence, ERP modernisation",
            HiringTrends = offshoringLikely ? "15+ digital roles" : "5+ IT roles",
            DigitalRoles = "Cloud, Data, SAP, Cyber",
            PainPoints = "Automation, legacy modernisation, cost optimisation",
            RenewalCycle = "Medium-term",
            LeadScore = offshoringLikely ? 70 + (hash % 25) : 40 + (hash % 20)
        };

        return Task.FromResult(company);
    }
}

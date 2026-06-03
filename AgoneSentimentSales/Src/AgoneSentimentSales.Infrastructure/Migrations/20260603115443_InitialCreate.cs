using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgoneSentimentSales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sentimentsales");

            migrationBuilder.CreateTable(
                name: "ApiRequestLogs",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ClientIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRequestLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Companies",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ticker = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IndustryGroup = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarketCapGbpB = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HqLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OffshoringStatus = table.Column<int>(type: "int", nullable: false),
                    PrimaryOffshoreLocations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasAsiaSubsidiary = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastResearchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataSourceNotes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResearchJobs",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TargetCompanyCount = table.Column<int>(type: "int", nullable: false),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputFilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutiveContacts",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LseCompanyId = table.Column<int>(type: "int", nullable: false),
                    ExecutiveName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleType = table.Column<int>(type: "int", nullable: false),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedEmailFormat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AreasOfResponsibility = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutiveContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutiveContacts_Companies_LseCompanyId",
                        column: x => x.LseCompanyId,
                        principalSchema: "sentimentsales",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItBudgets",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LseCompanyId = table.Column<int>(type: "int", nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    AnnualRevenueGbpB = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedItBudgetGbpM = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ItAsPercentOfRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CapexGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OpexGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OffshoreResourceCostGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OnshoreResourceCostGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CloudInfrastructureGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApplicationLicensingGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ApplicationSupportGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataAndAiProjectsGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EndUserComputingGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CyberSecurityGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ManagedServicesGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimationMethodology = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItBudgets_Companies_LseCompanyId",
                        column: x => x.LseCompanyId,
                        principalSchema: "sentimentsales",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadGenerationData",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LseCompanyId = table.Column<int>(type: "int", nullable: false),
                    AsiaOperations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItAnnouncements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HiringTrends = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DigitalRoles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PainPoints = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenewalCycle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadScore = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadGenerationData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadGenerationData_Companies_LseCompanyId",
                        column: x => x.LseCompanyId,
                        principalSchema: "sentimentsales",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutsourcingPartners",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LseCompanyId = table.Column<int>(type: "int", nullable: false),
                    PrimaryPartners = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondaryPartners = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OffshoreDeliveryCenters = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedAnnualContractGbpM = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PartnershipDuration = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutsourcingPartners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutsourcingPartners_Companies_LseCompanyId",
                        column: x => x.LseCompanyId,
                        principalSchema: "sentimentsales",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechnologyStrategies",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LseCompanyId = table.Column<int>(type: "int", nullable: false),
                    DigitalMaturity = table.Column<int>(type: "int", nullable: false),
                    AiMlPrograms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CloudStrategy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyTechInitiatives = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AutomationFocus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataAnalytics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DigitalTransformationEvidence = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyStrategies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnologyStrategies_Companies_LseCompanyId",
                        column: x => x.LseCompanyId,
                        principalSchema: "sentimentsales",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Rank",
                schema: "sentimentsales",
                table: "Companies",
                column: "Rank");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Ticker",
                schema: "sentimentsales",
                table: "Companies",
                column: "Ticker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutiveContacts_LseCompanyId",
                schema: "sentimentsales",
                table: "ExecutiveContacts",
                column: "LseCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ItBudgets_LseCompanyId",
                schema: "sentimentsales",
                table: "ItBudgets",
                column: "LseCompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadGenerationData_LseCompanyId",
                schema: "sentimentsales",
                table: "LeadGenerationData",
                column: "LseCompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutsourcingPartners_LseCompanyId",
                schema: "sentimentsales",
                table: "OutsourcingPartners",
                column: "LseCompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyStrategies_LseCompanyId",
                schema: "sentimentsales",
                table: "TechnologyStrategies",
                column: "LseCompanyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiRequestLogs",
                schema: "sentimentsales");

            migrationBuilder.DropTable(
                name: "ExecutiveContacts",
                schema: "sentimentsales");

            migrationBuilder.DropTable(
                name: "ItBudgets",
                schema: "sentimentsales");

            migrationBuilder.DropTable(
                name: "LeadGenerationData",
                schema: "sentimentsales");

            migrationBuilder.DropTable(
                name: "OutsourcingPartners",
                schema: "sentimentsales");

            migrationBuilder.DropTable(
                name: "ResearchJobs",
                schema: "sentimentsales");

            migrationBuilder.DropTable(
                name: "TechnologyStrategies",
                schema: "sentimentsales");

            migrationBuilder.DropTable(
                name: "Companies",
                schema: "sentimentsales");
        }
    }
}

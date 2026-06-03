using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgoneSentimentSales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceExtractionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourcedDataPoints",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LseCompanyId = table.Column<int>(type: "int", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FieldValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourcedDataPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourcedDataPoints_Companies_LseCompanyId",
                        column: x => x.LseCompanyId,
                        principalSchema: "sentimentsales",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceExtractionEvents",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResearchJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LseCompanyId = table.Column<int>(type: "int", nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtractedValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawSnippet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfidenceScore = table.Column<double>(type: "float", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceExtractionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceExtractionEvents_Companies_LseCompanyId",
                        column: x => x.LseCompanyId,
                        principalSchema: "sentimentsales",
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SourceExtractionEvents_ResearchJobs_ResearchJobId",
                        column: x => x.ResearchJobId,
                        principalSchema: "sentimentsales",
                        principalTable: "ResearchJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourcedDataPoints_LseCompanyId",
                schema: "sentimentsales",
                table: "SourcedDataPoints",
                column: "LseCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SourcedDataPoints_LseCompanyId_FieldName",
                schema: "sentimentsales",
                table: "SourcedDataPoints",
                columns: new[] { "LseCompanyId", "FieldName" });

            migrationBuilder.CreateIndex(
                name: "IX_SourceExtractionEvents_LseCompanyId",
                schema: "sentimentsales",
                table: "SourceExtractionEvents",
                column: "LseCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceExtractionEvents_ResearchJobId",
                schema: "sentimentsales",
                table: "SourceExtractionEvents",
                column: "ResearchJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourcedDataPoints",
                schema: "sentimentsales");

            migrationBuilder.DropTable(
                name: "SourceExtractionEvents",
                schema: "sentimentsales");
        }
    }
}

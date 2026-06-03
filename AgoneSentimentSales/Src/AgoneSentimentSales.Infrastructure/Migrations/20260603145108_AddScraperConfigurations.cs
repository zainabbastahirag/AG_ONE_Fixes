using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgoneSentimentSales.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScraperConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScraperConfigurations",
                schema: "sentimentsales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseUrlTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MaxItemsToScrape = table.Column<int>(type: "int", nullable: false),
                    DelayMsMin = table.Column<int>(type: "int", nullable: false),
                    DelayMsMax = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScraperConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScraperConfigurations_SourceType",
                schema: "sentimentsales",
                table: "ScraperConfigurations",
                column: "SourceType",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScraperConfigurations",
                schema: "sentimentsales");
        }
    }
}

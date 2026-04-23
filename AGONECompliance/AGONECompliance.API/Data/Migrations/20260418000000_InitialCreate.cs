using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGONECompliance.API.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "compliance");

            migrationBuilder.CreateTable(
                name: "Projects", schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(maxLength: 300, nullable: false),
                    Description = table.Column<string>(maxLength: 1000, nullable: true),
                    Status = table.Column<int>(nullable: false),
                    TotalChecks = table.Column<int>(nullable: false),
                    CompliantCount = table.Column<int>(nullable: false),
                    NonCompliantCount = table.Column<int>(nullable: false),
                    PendingCount = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Projects", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "Documents", schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProjectId = table.Column<Guid>(nullable: false),
                    FileName = table.Column<string>(maxLength: 500, nullable: false),
                    BlobPath = table.Column<string>(maxLength: 1000, nullable: false),
                    BlobUrl = table.Column<string>(maxLength: 2000, nullable: true),
                    DocType = table.Column<int>(nullable: false),
                    ExtractedText = table.Column<string>(nullable: true),
                    PageCount = table.Column<int>(nullable: true),
                    ExtractionStatus = table.Column<int>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey("FK_Documents_Projects", x => x.ProjectId, "Projects", "Id", principalSchema: "compliance");
                });

            migrationBuilder.CreateTable(
                name: "Rules", schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProjectId = table.Column<Guid>(nullable: false),
                    RuleNumber = table.Column<int>(nullable: false),
                    Code = table.Column<string>(maxLength: 100, nullable: false),
                    Paragraph = table.Column<string>(maxLength: 500, nullable: false),
                    Requirement = table.Column<string>(maxLength: 4000, nullable: false),
                    Complexity = table.Column<int>(nullable: false),
                    Group = table.Column<string>(maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey("FK_Rules_Projects", x => x.ProjectId, "Projects", "Id", principalSchema: "compliance");
                });

            migrationBuilder.CreateTable(
                name: "Checks", schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProjectId = table.Column<Guid>(nullable: false),
                    RuleId = table.Column<Guid>(nullable: false),
                    ProspectusDocId = table.Column<Guid>(nullable: true),
                    Result = table.Column<int>(nullable: false),
                    Finding = table.Column<string>(maxLength: 4000, nullable: true),
                    Evidence = table.Column<string>(maxLength: 4000, nullable: true),
                    PageReference = table.Column<string>(maxLength: 200, nullable: true),
                    SectionReference = table.Column<string>(maxLength: 500, nullable: true),
                    ConfidenceScore = table.Column<double>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checks", x => x.Id);
                    table.ForeignKey("FK_Checks_Projects", x => x.ProjectId, "Projects", "Id", principalSchema: "compliance");
                    table.ForeignKey("FK_Checks_Rules", x => x.RuleId, "Rules", "Id", principalSchema: "compliance");
                    table.ForeignKey("FK_Checks_Documents", x => x.ProspectusDocId, "Documents", "Id", principalSchema: "compliance");
                });

            migrationBuilder.CreateIndex("IX_Documents_ProjectId", "Documents", "ProjectId", schema: "compliance");
            migrationBuilder.CreateIndex("IX_Rules_ProjectId_RuleNumber", "Rules", new[] { "ProjectId", "RuleNumber" }, schema: "compliance");
            migrationBuilder.CreateIndex("IX_Checks_ProjectId_Result", "Checks", new[] { "ProjectId", "Result" }, schema: "compliance");
            migrationBuilder.CreateIndex("IX_Projects_CreatedAt", "Projects", "CreatedAt", schema: "compliance");

            migrationBuilder.CreateTable(
                name: "JobActivities", schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ProjectId = table.Column<Guid>(nullable: false),
                    JobType = table.Column<string>(maxLength: 100, nullable: false),
                    Step = table.Column<string>(maxLength: 300, nullable: false),
                    Status = table.Column<string>(maxLength: 50, nullable: false),
                    Message = table.Column<string>(maxLength: 2000, nullable: true),
                    ErrorDetail = table.Column<string>(maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<int>(nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_JobActivities", x => x.Id); });

            migrationBuilder.CreateIndex("IX_JobActivities_ProjectId_StartedAt", "JobActivities", new[] { "ProjectId", "StartedAt" }, schema: "compliance");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("JobActivities", "compliance");
            migrationBuilder.DropTable("Checks", "compliance");
            migrationBuilder.DropTable("Rules", "compliance");
            migrationBuilder.DropTable("Documents", "compliance");
            migrationBuilder.DropTable("Projects", "compliance");
        }
    }
}

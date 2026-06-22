using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VidCV.Web.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CvProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Skills = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Experience = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Education = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CvFileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CvFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VideoScript = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    VideoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VideoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CvProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BackgroundColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccentColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TextColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FontFamily = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CvProfiles_Email",
                table: "CvProfiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_CvProfiles_CreatedAt",
                table: "CvProfiles",
                column: "CreatedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CvProfiles");
            migrationBuilder.DropTable(name: "VideoTemplates");
        }
    }
}

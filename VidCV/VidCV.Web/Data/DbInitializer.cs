using Microsoft.EntityFrameworkCore;
using VidCV.Web.Models;

namespace VidCV.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        if (!await db.VideoTemplates.AnyAsync())
        {
            db.VideoTemplates.AddRange(
                new VideoTemplate
                {
                    Name = "Professional Dark",
                    Description = "Elegant dark navy theme with smooth animations",
                    BackgroundColor = "#0F172A",
                    AccentColor = "#3B82F6",
                    TextColor = "#F8FAFC",
                    FontFamily = "Inter",
                    DurationSeconds = 30,
                    IsActive = true
                },
                new VideoTemplate
                {
                    Name = "Modern Light",
                    Description = "Clean white theme with vibrant accents",
                    BackgroundColor = "#FFFFFF",
                    AccentColor = "#6366F1",
                    TextColor = "#1E293B",
                    FontFamily = "Inter",
                    DurationSeconds = 30,
                    IsActive = true
                },
                new VideoTemplate
                {
                    Name = "Creative Gradient",
                    Description = "Bold gradient background with dynamic text",
                    BackgroundColor = "#1E3A5F",
                    AccentColor = "#10B981",
                    TextColor = "#FFFFFF",
                    FontFamily = "Inter",
                    DurationSeconds = 30,
                    IsActive = true
                }
            );
            await db.SaveChangesAsync();
        }
    }
}

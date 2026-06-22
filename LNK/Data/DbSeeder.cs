using LNK.Configuration;
using LNK.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LNK.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        await db.Database.MigrateAsync();

        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        if (!await db.Settings.AnyAsync())
        {
            db.Settings.AddRange(
                new AppSetting { Key = "MaintenanceMode", Value = "false" },
                new AppSetting { Key = "DefaultOllamaModel", Value = "llama3.2" },
                new AppSetting { Key = "WelcomeMessage", Value = "Welcome to LNK" });
            await db.SaveChangesAsync();
        }

        if (!options.SeedDemoData) return;

        const string adminEmail = "admin@lnk.app";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = "LNK Admin",
                OnboardingCompleted = true
            };
            await userManager.CreateAsync(admin, "Lnk@Admin123!");
            await userManager.AddToRoleAsync(admin, "Admin");

            db.UserSettings.Add(new UserSettings
            {
                UserId = admin.Id,
                Industry = "SaaS",
                Topics = "Product, Growth, AI",
                Tone = "Professional",
                DailyPostTime = new TimeSpan(9, 0, 0)
            });
            db.Schedules.Add(new Schedule { UserId = admin.Id, IsActive = true, PostTime = new TimeSpan(9, 0, 0) });
            await db.SaveChangesAsync();
        }

        const string demoEmail = "demo@lnk.app";
        if (await userManager.FindByEmailAsync(demoEmail) == null)
        {
            var demo = new ApplicationUser
            {
                UserName = demoEmail,
                Email = demoEmail,
                EmailConfirmed = true,
                DisplayName = "Demo User",
                OnboardingCompleted = true
            };
            await userManager.CreateAsync(demo, "Lnk@Demo123!");
            db.UserSettings.Add(new UserSettings
            {
                UserId = demo.Id,
                Industry = "Technology",
                Topics = "Leadership, AI, Startups",
                Keywords = "innovation",
                Tone = "Executive",
                DailyPostTime = new TimeSpan(8, 30, 0)
            });
            db.Schedules.Add(new Schedule { UserId = demo.Id });
            db.Posts.Add(new Post
            {
                UserId = demo.Id,
                Title = "The future of professional content",
                Hook = "Your audience doesn't need more noise — they need clarity.",
                Content = "I've spent the last decade watching leaders struggle with consistency on LinkedIn.\n\nThe winners? They show up with one clear idea per day.",
                CallToAction = "What's the one idea you'll share today?",
                Hashtags = "#Leadership #LinkedIn #Growth",
                FullText = "",
                Status = "Ready",
                GeneratedAt = DateTime.UtcNow.AddHours(-2),
                ImageUrl = "https://picsum.photos/seed/lnk-demo/1200/630"
            });
            await db.SaveChangesAsync();
            var p = await db.Posts.FirstAsync(x => x.UserId == demo.Id);
            p.FullText = Helpers.PostFormatter.BuildFullText(p);
            await db.SaveChangesAsync();
        }
    }
}

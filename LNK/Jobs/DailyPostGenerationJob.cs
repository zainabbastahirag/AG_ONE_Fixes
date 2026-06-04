using LNK.Data;
using LNK.Models;
using LNK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LNK.Configuration;
using Quartz;

namespace LNK.Jobs;

[DisallowConcurrentExecution]
public class DailyPostGenerationJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyPostGenerationJob(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var generator = scope.ServiceProvider.GetRequiredService<IPostGenerationService>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DailyPostGenerationJob>>();

        var now = DateTime.UtcNow;
        var users = await db.Users
            .Include(u => u.Settings)
            .Include(u => u.Schedule)
            .Where(u => u.OnboardingCompleted && u.Settings != null)
            .Where(u => u.Schedule == null || u.Schedule.IsActive)
            .ToListAsync(context.CancellationToken);

        foreach (var user in users)
        {
            var settings = user.Settings!;
            if (!IsDueNow(settings, user.Schedule, now)) continue;

            try
            {
                var post = await generator.GenerateForUserAsync(user, settings, context.CancellationToken);
                var reviewUrl = $"{emailSettings.AppBaseUrl.TrimEnd('/')}/Posts/Review/{post.Id}";
                await email.SendPostEmailAsync(user, post, reviewUrl, context.CancellationToken);

                if (user.Schedule != null)
                {
                    user.Schedule.LastRunAt = now;
                    user.Schedule.NextRunAt = now.Date.AddDays(1).Add(settings.DailyPostTime);
                }
                await db.SaveChangesAsync(context.CancellationToken);
                logger.LogInformation("Daily post completed for {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily post failed for {UserId}", user.Id);
            }
        }
    }

    private static bool IsDueNow(UserSettings settings, Schedule? schedule, DateTime now)
    {
        var target = now.Date.Add(settings.DailyPostTime);
        return now >= target && now < target.AddMinutes(30);
    }
}

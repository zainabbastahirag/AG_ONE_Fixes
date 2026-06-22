using AGONECompliance.API.Data;
using AGONECompliance.Shared.Models;

namespace AGONECompliance.API.Services;

public class JobActivityService
{
    private readonly AppDbContext _db;

    public JobActivityService(AppDbContext db) => _db = db;

    public async Task<Guid> StartAsync(Guid projectId, string jobType, string step, string? message = null)
    {
        var activity = new JobActivity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            JobType = jobType,
            Step = step,
            Status = "Running",
            Message = message,
            StartedAt = DateTime.UtcNow
        };
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync();
        return activity.Id;
    }

    public async Task CompleteAsync(Guid activityId, string? message = null)
    {
        var a = await _db.Activities.FindAsync(activityId);
        if (a == null) return;
        a.Status = "Completed";
        a.Message = message ?? a.Message;
        a.CompletedAt = DateTime.UtcNow;
        a.DurationMs = (int)(a.CompletedAt.Value - a.StartedAt).TotalMilliseconds;
        await _db.SaveChangesAsync();
    }

    public async Task FailAsync(Guid activityId, string error, string? detail = null)
    {
        var a = await _db.Activities.FindAsync(activityId);
        if (a == null) return;
        a.Status = "Failed";
        a.Message = error;
        a.ErrorDetail = detail;
        a.CompletedAt = DateTime.UtcNow;
        a.DurationMs = (int)(a.CompletedAt.Value - a.StartedAt).TotalMilliseconds;
        await _db.SaveChangesAsync();
    }
}

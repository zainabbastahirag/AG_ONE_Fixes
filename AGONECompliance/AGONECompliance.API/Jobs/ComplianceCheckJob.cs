using AGONECompliance.API.Data;
using AGONECompliance.API.Services;
using AGONECompliance.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace AGONECompliance.API.Jobs;

[DisallowConcurrentExecution]
public class ComplianceCheckJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComplianceCheckJob> _log;

    public ComplianceCheckJob(IServiceScopeFactory scopeFactory, ILogger<ComplianceCheckJob> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var projectId = context.MergedJobDataMap.GetString("ProjectId");
        if (string.IsNullOrEmpty(projectId) || !Guid.TryParse(projectId, out var id))
        {
            _log.LogWarning("ComplianceCheckJob: invalid ProjectId");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var extraction = scope.ServiceProvider.GetRequiredService<DocumentExtractionService>();
        var checker = scope.ServiceProvider.GetRequiredService<ComplianceCheckService>();
        var activity = scope.ServiceProvider.GetRequiredService<JobActivityService>();

        // ── Step 1: Start job ──
        var jobLogId = await activity.StartAsync(id, "ComplianceCheck", "Job Started", "Compliance check pipeline initiated");

        try
        {
            // ── Step 2: Extract documents ──
            var pendingDocs = await db.Documents
                .Where(d => d.ProjectId == id && d.ExtractionStatus == JobStatus.Pending)
                .ToListAsync();

            if (pendingDocs.Count > 0)
            {
                var extractId = await activity.StartAsync(id, "Extraction", $"Extracting {pendingDocs.Count} document(s)",
                    $"Files: {string.Join(", ", pendingDocs.Select(d => d.FileName))}");

                foreach (var doc in pendingDocs)
                {
                    var fileId = await activity.StartAsync(id, "Extraction", $"Extracting: {doc.FileName}");
                    try
                    {
                        await extraction.ExtractTextAsync(doc.Id);
                        await activity.CompleteAsync(fileId, $"Extracted {doc.PageCount ?? 0} pages");
                    }
                    catch (Exception ex)
                    {
                        await activity.FailAsync(fileId, $"Extraction failed for {doc.FileName}", ex.Message);
                    }
                }

                await activity.CompleteAsync(extractId, $"All {pendingDocs.Count} documents processed");
            }

            // ── Step 3: Run compliance checks ──
            var checkId = await activity.StartAsync(id, "ComplianceCheck", "Running AI compliance evaluation");
            try
            {
                await checker.RunComplianceChecksAsync(id);

                var project = await db.Projects.FindAsync(id);
                await activity.CompleteAsync(checkId,
                    $"Done: {project?.CompliantCount ?? 0} compliant, {project?.NonCompliantCount ?? 0} non-compliant");
            }
            catch (Exception ex)
            {
                await activity.FailAsync(checkId, "Compliance evaluation failed", ex.Message);
                throw;
            }

            await activity.CompleteAsync(jobLogId, "Pipeline completed successfully");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ComplianceCheckJob failed for {Id}", id);
            await activity.FailAsync(jobLogId, "Pipeline failed", ex.ToString());
        }
    }
}

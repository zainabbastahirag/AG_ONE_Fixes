namespace AgoneSentimentSales.Domain.Interfaces;

public interface IReportsDataService
{
    Task<object> GetViewDataAsync(string viewId, Guid? jobId = null, CancellationToken cancellationToken = default);
    IReadOnlyList<ReportViewDefinition> GetViewDefinitions();
}

public record ReportViewDefinition(string ViewId, string Title, string Description, string Icon, string ExcelSheetName);

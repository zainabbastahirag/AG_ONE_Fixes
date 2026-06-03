using AgoneSentimentSales.Core.Entities;

namespace AgoneSentimentSales.Core.Interfaces;

public interface IExcelExportService
{
    Task<byte[]> ExportWorkbookAsync(IReadOnlyList<LseCompany> companies, CancellationToken cancellationToken = default);
    Task<string> SaveWorkbookAsync(IReadOnlyList<LseCompany> companies, string outputDirectory, CancellationToken cancellationToken = default);
}

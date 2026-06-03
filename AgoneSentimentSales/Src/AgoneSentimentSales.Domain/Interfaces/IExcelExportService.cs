using AgoneSentimentSales.Domain.Entities;

namespace AgoneSentimentSales.Domain.Interfaces;

public interface IExcelExportService
{
    Task<byte[]> ExportWorkbookAsync(IReadOnlyList<LseCompany> companies, IReadOnlyList<SourceExtractionEvent>? extractions = null, CancellationToken cancellationToken = default);
    Task<string> SaveWorkbookAsync(IReadOnlyList<LseCompany> companies, IReadOnlyList<SourceExtractionEvent>? extractions, string outputDirectory, CancellationToken cancellationToken = default);
}

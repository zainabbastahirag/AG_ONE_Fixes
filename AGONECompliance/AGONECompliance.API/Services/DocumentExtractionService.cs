using AGONECompliance.API.Data;
using AGONECompliance.Shared.Enums;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;

namespace AGONECompliance.API.Services;

public class DocumentExtractionService
{
    private readonly AppDbContext _db;
    private readonly BlobServiceClient _blob;
    private readonly IConfiguration _cfg;
    private readonly ILogger<DocumentExtractionService> _log;

    public DocumentExtractionService(AppDbContext db, BlobServiceClient blob, IConfiguration cfg, ILogger<DocumentExtractionService> log)
    {
        _db = db;
        _blob = blob;
        _cfg = cfg;
        _log = log;
    }

    public async Task<string> UploadToBlobAsync(Stream fileStream, string fileName, string projectId, string docType, CancellationToken ct = default)
    {
        var containerName = _cfg["Azure:BlobContainer"] ?? "compliance-docs";
        var container = _blob.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobPath = $"{projectId}/{docType}/{Guid.NewGuid()}_{fileName}";
        var blobClient = container.GetBlobClient(blobPath);
        await blobClient.UploadAsync(fileStream, overwrite: true, ct);

        return blobPath;
    }

    public async Task ExtractTextAsync(Guid documentId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FindAsync(new object[] { documentId }, ct);
        if (doc == null) return;

        doc.ExtractionStatus = JobStatus.Processing;
        await _db.SaveChangesAsync(ct);

        try
        {
            var endpoint = _cfg["Azure:DocIntelligenceEndpoint"];
            var key = _cfg["Azure:DocIntelligenceKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
            {
                _log.LogWarning("Azure Doc Intelligence not configured — using placeholder extraction");
                doc.ExtractedText = $"[Placeholder] Text extraction pending for {doc.FileName}. Configure Azure Doc Intelligence for real extraction.";
                doc.PageCount = 1;
                doc.ExtractionStatus = JobStatus.Completed;
                await _db.SaveChangesAsync(ct);
                return;
            }

            var containerName = _cfg["Azure:BlobContainer"] ?? "compliance-docs";
            var container = _blob.GetBlobContainerClient(containerName);
            var blobClient = container.GetBlobClient(doc.BlobPath);
            var download = await blobClient.DownloadContentAsync(ct);
            var pdfBytes = download.Value.Content.ToArray();

            var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed, "prebuilt-layout",
                new MemoryStream(pdfBytes), cancellationToken: ct);

            var result = operation.Value;
            var textParts = new List<string>();
            foreach (var page in result.Pages)
            {
                var pageText = string.Join(" ", page.Lines.Select(l => l.Content));
                textParts.Add($"[Page {page.PageNumber}]\n{pageText}");
            }

            doc.ExtractedText = string.Join("\n\n", textParts);
            doc.PageCount = result.Pages.Count;
            doc.ExtractionStatus = JobStatus.Completed;

            _log.LogInformation("Extracted {Pages} pages from {File}", result.Pages.Count, doc.FileName);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Extraction failed for {File}", doc.FileName);
            doc.ExtractionStatus = JobStatus.Failed;
            doc.ExtractedText = $"Extraction failed: {ex.Message}";
        }

        await _db.SaveChangesAsync(ct);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// NEW SERVICE: ExtractedTextBlobStore.cs
// Handles all extracted text storage in Azure Blob instead of SQL.
// Register in DI: services.AddScoped<IExtractedTextBlobStore, ExtractedTextBlobStore>();
// ═══════════════════════════════════════════════════════════════════════

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AGOne.Spot.Services;

public interface IExtractedTextBlobStore
{
    /// <summary>
    /// Save extracted text to Blob. Returns the blob path stored.
    /// </summary>
    Task<string> SaveExtractedTextAsync(string companyId, string fileUID, string text, CancellationToken ct = default);

    /// <summary>
    /// Read extracted text from Blob on demand. Returns empty string if not found.
    /// </summary>
    Task<string> GetExtractedTextAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Check if extracted text already exists in Blob for this file.
    /// </summary>
    Task<bool> ExistsAsync(string companyId, string fileUID, CancellationToken ct = default);

    /// <summary>
    /// Delete extracted text from Blob (cleanup).
    /// </summary>
    Task DeleteAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Build the standard blob path for a file's extracted text.
    /// </summary>
    string BuildBlobPath(string companyId, string fileUID);
}

public class ExtractedTextBlobStore : IExtractedTextBlobStore
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<ExtractedTextBlobStore> _log;
    private const string Prefix = "extracted-text";

    public ExtractedTextBlobStore(
        BlobServiceClient blobService,
        SpotConfiguration cfg,
        ILogger<ExtractedTextBlobStore> log)
    {
        _container = blobService.GetBlobContainerClient(cfg.BlobContainer);
        _log = log;
    }

    public string BuildBlobPath(string companyId, string fileUID)
        => $"{Prefix}/{companyId}/{fileUID}.json";

    public async Task<string> SaveExtractedTextAsync(
        string companyId, string fileUID, string text, CancellationToken ct = default)
    {
        var blobPath = BuildBlobPath(companyId, fileUID);
        var blobClient = _container.GetBlobClient(blobPath);

        var bytes = Encoding.UTF8.GetBytes(text);
        await blobClient.UploadAsync(
            new BinaryData(bytes),
            overwrite: true,
            ct);

        _log.LogInformation(
            "[ExtractedTextStore] Saved {Chars} chars to {Path}",
            text.Length, blobPath);

        return blobPath;
    }

    public async Task<string> GetExtractedTextAsync(string blobPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(blobPath))
            return string.Empty;

        var blobClient = _container.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync(ct))
        {
            _log.LogWarning("[ExtractedTextStore] Blob not found: {Path}", blobPath);
            return string.Empty;
        }

        var response = await blobClient.DownloadContentAsync(ct);
        return response.Value.Content.ToString();
    }

    public async Task<bool> ExistsAsync(string companyId, string fileUID, CancellationToken ct = default)
    {
        var blobPath = BuildBlobPath(companyId, fileUID);
        var blobClient = _container.GetBlobClient(blobPath);
        return await blobClient.ExistsAsync(ct);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(blobPath)) return;
        var blobClient = _container.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }
}

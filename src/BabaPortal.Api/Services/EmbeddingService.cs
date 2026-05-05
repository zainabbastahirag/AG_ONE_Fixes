using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace BabaPortal.Api.Services;

/// Produces dense float embeddings.
/// Primary path: Ollama embeddings endpoint (configurable model, e.g. nomic-embed-text).
/// Fallback path: deterministic local hash-based bag-of-words embedding so the system
/// works even when Ollama is offline (graceful degradation).
public class EmbeddingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<EmbeddingService> _log;

    public const int FallbackDim = 256;

    public EmbeddingService(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<EmbeddingService> log)
    {
        _httpFactory = httpFactory;
        _cfg = cfg;
        _log = log;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var ollamaBase = _cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = _cfg["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var resp = await client.PostAsJsonAsync($"{ollamaBase}/api/embeddings",
                new { model, prompt = text }, ct);
            if (resp.IsSuccessStatusCode)
            {
                var payload = await resp.Content.ReadFromJsonAsync<OllamaEmbedResponse>(cancellationToken: ct);
                if (payload?.embedding is { Length: > 0 } e) return e;
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Ollama embedding unavailable, using fallback hashing.");
        }
        return HashEmbed(text, FallbackDim);
    }

    /// Deterministic local fallback: tokenize, hash to bins, l2-normalize.
    public static float[] HashEmbed(string text, int dim)
    {
        var v = new float[dim];
        if (string.IsNullOrWhiteSpace(text)) return v;
        var tokens = text.ToLowerInvariant()
            .Split(new[] { ' ', '\n', '\t', '.', ',', '!', '?', ';', ':', '\'', '"', '(', ')', '[', ']', '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries);
        foreach (var tok in tokens)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(tok));
            var idx = (BitConverter.ToUInt32(bytes, 0)) % (uint)dim;
            var sign = (bytes[4] & 1) == 0 ? 1f : -1f;
            v[idx] += sign;
        }
        double norm = 0; for (int i = 0; i < dim; i++) norm += v[i] * v[i];
        norm = Math.Sqrt(norm);
        if (norm > 1e-8) for (int i = 0; i < dim; i++) v[i] = (float)(v[i] / norm);
        return v;
    }

    public static float CosineSim(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        if (na < 1e-12 || nb < 1e-12) return 0;
        return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
    }

    public static byte[] ToBytes(float[] v)
    {
        var bytes = new byte[v.Length * 4];
        Buffer.BlockCopy(v, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] FromBytes(byte[] b, int dim)
    {
        var v = new float[dim];
        Buffer.BlockCopy(b, 0, v, 0, Math.Min(b.Length, dim * 4));
        return v;
    }

    private record OllamaEmbedResponse(float[]? embedding);
}

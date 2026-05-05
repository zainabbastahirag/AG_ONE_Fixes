using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BabaPortal.Api.Services;

/// Streams Ollama chat responses chunk-by-chunk so the UI feels real-time.
public class OllamaService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<OllamaService> _log;

    public OllamaService(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<OllamaService> log)
    {
        _httpFactory = httpFactory; _cfg = cfg; _log = log;
    }

    public string DefaultModel => _cfg["Ollama:ChatModel"] ?? "llama3.2";
    public string BaseUrl => _cfg["Ollama:BaseUrl"] ?? "http://localhost:11434";

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<OllamaMessage> messages,
        string? model = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = new
        {
            model = model ?? DefaultModel,
            stream = true,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            options = new { temperature = 0.7, num_predict = 512 }
        };

        var (resp, stream, errorMessage) = await TryOpenStreamAsync(body, ct);
        if (errorMessage is not null) { yield return errorMessage; yield break; }

        using var _resp = resp;
        using var _stream = stream;
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            OllamaChatChunk? chunk = null;
            try { chunk = JsonSerializer.Deserialize<OllamaChatChunk>(line); }
            catch (Exception ex) { _log.LogDebug(ex, "skip malformed chunk"); }
            if (chunk?.message?.content is { Length: > 0 } piece)
                yield return piece;
            if (chunk?.done == true) yield break;
        }
    }

    private async Task<(HttpResponseMessage? resp, Stream? stream, string? error)> TryOpenStreamAsync(object body, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(10);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/chat");
            req.Content = JsonContent.Create(body);
            var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                _log.LogWarning("Ollama chat failed {Status}: {Err}", resp.StatusCode, err);
                resp.Dispose();
                return (null, null, $"[BABA is offline. Start Ollama and pull the model. Details: {(int)resp.StatusCode}]");
            }
            var stream = await resp.Content.ReadAsStreamAsync(ct);
            return (resp, stream, null);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Ollama stream init failed");
            return (null, null, $"[BABA cannot reach Ollama at {BaseUrl}. Start Ollama and pull a model like '{DefaultModel}'.]");
        }
    }

    public record OllamaMessage(string Role, string Content);

    private record OllamaChatChunk(
        [property: JsonPropertyName("model")] string? model,
        [property: JsonPropertyName("message")] OllamaInner? message,
        [property: JsonPropertyName("done")] bool done);

    private record OllamaInner(
        [property: JsonPropertyName("role")] string? role,
        [property: JsonPropertyName("content")] string? content);
}

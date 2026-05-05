using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.Baba.Web.Services;

public class OllamaService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<OllamaService> _log;

    public OllamaService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<OllamaService> log)
    {
        _httpFactory = httpFactory;
        _config = config;
        _log = log;
    }

    public string BaseUrl => _config["Ollama:BaseUrl"] ?? "http://localhost:11434";
    public string ChatModel => _config["Ollama:ChatModel"] ?? _config["Ollama:Model"] ?? "llama3.2";

    /// Legacy non-streaming generate kept for /api/ask compatibility.
    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            var body = new
            {
                model = ChatModel,
                system = systemPrompt,
                prompt = userPrompt,
                stream = false,
                options = new { temperature = 0.7, top_p = 0.9, num_predict = 300 }
            };

            var response = await client.PostAsync(
                $"{BaseUrl}/api/generate",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                return json.GetProperty("response").GetString() ?? "I sense a disturbance... please try again.";
            }

            _log.LogWarning("Ollama returned {Status}", response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning("Ollama not reachable: {Msg}", ex.Message);
        }
        catch (TaskCanceledException)
        {
            _log.LogWarning("Ollama request timed out");
        }

        return GenerateFallback(userPrompt);
    }

    /// Streams Ollama chat responses chunk-by-chunk so the UI feels real-time.
    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatTurn> messages,
        string? model = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = new
        {
            model = model ?? ChatModel,
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
                return (null, null, GenerateFallback("offline"));
            }
            var stream = await resp.Content.ReadAsStreamAsync(ct);
            return (resp, stream, null);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Ollama stream init failed");
            return (null, null, GenerateFallback("offline"));
        }
    }

    private static string GenerateFallback(string prompt)
    {
        var lower = (prompt ?? string.Empty).ToLowerInvariant();

        if (lower == "offline")
            return "The cosmos hum quietly tonight, seeker — Ollama is not reachable. Run 'ollama serve' and pull the model, then ask again.";

        if (lower.Contains("name") && (lower.Contains("my") || lower.Contains("i am") || lower.Contains("i'm")))
            return "A beautiful name, seeker. I shall remember you. What wisdom do you seek today?";

        if (lower.Contains("hello") || lower.Contains("hi") || lower.Contains("hey"))
            return "Welcome, seeker. I am AI Baba-G — ask me anything, and together we shall explore the answers.";

        if (lower.Contains("meaning") || lower.Contains("life") || lower.Contains("purpose"))
            return "Ah, the eternal question. Purpose is not found — it is created through the choices you make each day. What calls to your heart?";

        if (lower.Contains("help") || lower.Contains("advice"))
            return "I am here to guide you, not to decide for you. Share what troubles you, and let us find clarity together.";

        if (lower.Contains("thank"))
            return "The gratitude is mine, seeker. Your questions light the path for both of us.";

        return "An interesting question, seeker. The wisest answers often come from within — but let me offer this: stay curious, stay humble, and the universe reveals its secrets in time.";
    }

    public record ChatTurn(string Role, string Content);

    private record OllamaChatChunk(
        [property: JsonPropertyName("model")] string? model,
        [property: JsonPropertyName("message")] OllamaInner? message,
        [property: JsonPropertyName("done")] bool done);

    private record OllamaInner(
        [property: JsonPropertyName("role")] string? role,
        [property: JsonPropertyName("content")] string? content);
}

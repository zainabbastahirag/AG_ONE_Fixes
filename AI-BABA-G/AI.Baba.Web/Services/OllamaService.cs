using System.Text;
using System.Text.Json;

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

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var baseUrl = _config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = _config["Ollama:Model"] ?? "llama3.2";

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            var body = new
            {
                model,
                system = systemPrompt,
                prompt = userPrompt,
                stream = false,
                options = new { temperature = 0.7, top_p = 0.9, num_predict = 300 }
            };

            var response = await client.PostAsync(
                $"{baseUrl}/api/generate",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
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

    private static string GenerateFallback(string prompt)
    {
        var lower = prompt.ToLower();

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
}

using System.Net.Http.Json;
using System.Text.Json;
using LNK.Configuration;
using Microsoft.Extensions.Options;

namespace LNK.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _http;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaService> _logger;

    public OllamaService(HttpClient http, IOptions<OllamaSettings> settings, ILogger<OllamaService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
        _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new { model = _settings.Model, prompt, stream = false };
            var response = await _http.PostAsJsonAsync("api/generate", body, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (json.TryGetProperty("response", out var text))
                return text.GetString()?.Trim() ?? "";
            return "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama unavailable, using fallback content");
            return "";
        }
    }
}

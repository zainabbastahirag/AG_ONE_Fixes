using AgoneSentimentSales.Domain.Interfaces;
using AgoneSentimentSales.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgoneSentimentSales.Infrastructure.Services;

/// <summary>
/// Azure OpenAI integration point. MVP returns structured placeholder when not configured.
/// </summary>
public class OpenAIChatService : IChatService
{
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAIChatService> _logger;

    public OpenAIChatService(IOptions<OpenAISettings> settings, ILogger<OpenAIChatService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<string> ChatWithTemplateAsync(string userMessage, string promptKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogWarning("OpenAI not configured; returning heuristic response for {PromptKey}", promptKey);
            return Task.FromResult($"[MVP-Heuristic:{promptKey}] Processed query length {userMessage.Length}. Configure OpenAI:ApiKey for live enrichment.");
        }

        // Production: call Azure OpenAI REST API with prompt template from DB
        return Task.FromResult($"[Azure-OpenAI:{_settings.DeploymentName}] Response placeholder.");
    }
}

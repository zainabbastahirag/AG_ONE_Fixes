namespace AgoneSentimentSales.Infrastructure.Configuration;

public class OpenAISettings
{
    public const string SectionName = "OpenAI";
    public bool UseAzure { get; set; } = true;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
}

namespace AgoneSentimentSales.Domain.Interfaces;

public interface IChatService
{
    Task<string> ChatWithTemplateAsync(string userMessage, string promptKey, CancellationToken cancellationToken = default);
}

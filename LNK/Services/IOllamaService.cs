namespace LNK.Services;

public interface IOllamaService
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}

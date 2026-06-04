using LNK.Models;

namespace LNK.Services;

public interface IPostGenerationService
{
    Task<Post> GenerateForUserAsync(ApplicationUser user, UserSettings settings, CancellationToken cancellationToken = default);
}

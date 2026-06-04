using LNK.Models;

namespace LNK.Services;

public interface IEmailService
{
    Task<bool> SendPostEmailAsync(ApplicationUser user, Post post, string reviewUrl, CancellationToken cancellationToken = default);
}

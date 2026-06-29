using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

/// <summary>Abstraction over the delivery channel (SMTP / Microsoft Graph).</summary>
public interface IEmailSender : IDisposable
{
    Task SendAsync(
        Recipient recipient,
        string subject,
        string htmlBody,
        IReadOnlyList<InlineImage> inlineImages,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null);
}

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

/// <summary>
/// Sends a rendered HTML email over SMTP using MailKit, embedding the Nexa image inline via a Content-ID.
/// </summary>
public sealed class EmailSender : IDisposable
{
    private readonly SmtpConfig _smtp;
    private readonly SenderConfig _sender;
    private readonly SmtpClient _client = new();
    private bool _connected;

    public EmailSender(SmtpConfig smtp, SenderConfig sender)
    {
        _smtp = smtp;
        _sender = sender;
        _client.Timeout = Math.Max(30, _smtp.TimeoutSeconds) * 1000;
    }

    public async Task ConnectAsync()
    {
        if (_connected) return;

        await _client.ConnectAsync(_smtp.Host, _smtp.Port, ResolveSecurity());

        if (!string.IsNullOrWhiteSpace(_smtp.Username))
            await _client.AuthenticateAsync(_smtp.Username, _smtp.Password);

        _connected = true;
    }

    public async Task SendAsync(
        Recipient recipient,
        string subject,
        string htmlBody,
        string nexaImagePath,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_sender.Name, _sender.Email));
        message.To.Add(new MailboxAddress(recipient.Name ?? recipient.Email, recipient.Email));
        message.Subject = subject;

        AddAddresses(message.Cc, cc);
        AddAddresses(message.Bcc, bcc);

        var builder = new BodyBuilder { HtmlBody = htmlBody };

        if (File.Exists(nexaImagePath) && htmlBody.Contains($"cid:{EmailTemplateRenderer.NexaImageContentId}"))
        {
            var image = builder.LinkedResources.Add(nexaImagePath);
            image.ContentId = EmailTemplateRenderer.NexaImageContentId;
            ((MimePart)image).ContentTransferEncoding = ContentEncoding.Base64;
        }

        message.Body = builder.ToMessageBody();

        await ConnectAsync();
        await _client.SendAsync(message);
    }

    private SecureSocketOptions ResolveSecurity()
    {
        var mode = (_smtp.Security ?? "").Trim().ToLowerInvariant();
        return mode switch
        {
            "starttls" => SecureSocketOptions.StartTls,
            "ssl" or "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "none" => SecureSocketOptions.None,
            "auto" => SecureSocketOptions.Auto,
            _ => _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect,
        };
    }

    private static void AddAddresses(InternetAddressList list, IEnumerable<string>? addresses)
    {
        if (addresses is null) return;
        foreach (var a in addresses)
        {
            var trimmed = a.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Contains('@'))
                list.Add(MailboxAddress.Parse(trimmed));
        }
    }

    public void Dispose()
    {
        try
        {
            if (_client.IsConnected)
                _client.Disconnect(true);
        }
        catch
        {
            /* best-effort disconnect */
        }
        _client.Dispose();
    }
}

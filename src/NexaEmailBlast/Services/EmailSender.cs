using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Utils;
using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

/// <summary>
/// Sends a rendered HTML email over SMTP using MailKit, embedding the Nexa image inline via a Content-ID.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpConfig _smtp;
    private readonly SenderConfig _sender;
    private readonly SmtpClient _client = new();
    private bool _connected;

    public SmtpEmailSender(SmtpConfig smtp, SenderConfig sender)
    {
        _smtp = smtp;
        _sender = sender;
        _client.Timeout = Math.Max(30, _smtp.TimeoutSeconds) * 1000;
    }

    public async Task ConnectAsync()
    {
        if (_connected) return;

        if (!_client.IsConnected)
            await _client.ConnectAsync(_smtp.Host, _smtp.Port, ResolveSecurity());

        if (!string.IsNullOrWhiteSpace(_smtp.Username) && !_client.IsAuthenticated)
            await _client.AuthenticateAsync(_smtp.Username, _smtp.Password);

        _connected = true;
    }

    public async Task SendAsync(
        Recipient recipient,
        string subject,
        string htmlBody,
        IReadOnlyList<InlineImage> inlineImages,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null)
    {
        var message = BuildMimeMessage(_sender, recipient, subject, htmlBody, inlineImages, cc, bcc);
        await ConnectAsync();
        await _client.SendAsync(message);
    }

    /// <summary>Builds the MIME message (exposed so it can be inspected without sending).</summary>
    public static MimeMessage BuildMimeMessage(
        SenderConfig sender,
        Recipient recipient,
        string subject,
        string htmlBody,
        IReadOnlyList<InlineImage> inlineImages,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(sender.Name, sender.Email));
        message.To.Add(new MailboxAddress(recipient.Name ?? recipient.Email, recipient.Email));
        message.Subject = subject;

        AddAddresses(message.Cc, cc);
        AddAddresses(message.Bcc, bcc);

        var builder = new BodyBuilder { HtmlBody = htmlBody };

        foreach (var img in inlineImages)
        {
            if (!File.Exists(img.Path)) continue;
            var resource = builder.LinkedResources.Add(img.Path);
            resource.ContentId = img.ContentId;
            ((MimePart)resource).ContentTransferEncoding = ContentEncoding.Base64;
        }

        message.Body = builder.ToMessageBody();
        return message;
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

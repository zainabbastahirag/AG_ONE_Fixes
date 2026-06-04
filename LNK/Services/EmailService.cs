using LNK.Configuration;
using LNK.Data;
using LNK.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LNK.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _email;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> email,
        ApplicationDbContext db,
        IWebHostEnvironment env,
        ILogger<EmailService> logger)
    {
        _email = email.Value;
        _db = db;
        _env = env;
        _logger = logger;
    }

    public async Task<bool> SendPostEmailAsync(ApplicationUser user, Post post, string reviewUrl, CancellationToken cancellationToken = default)
    {
        var subject = $"Your LinkedIn post is ready — {post.Title}";
        var log = new EmailLog { UserId = user.Id, PostId = post.Id, ToEmail = user.Email!, Subject = subject };

        try
        {
            var templatePath = Path.Combine(_env.ContentRootPath, "EmailTemplates", "DailyPost.html");
            var html = await File.ReadAllTextAsync(templatePath, cancellationToken);
            html = html
                .Replace("{{UserName}}", user.DisplayName ?? user.Email ?? "there")
                .Replace("{{Hook}}", System.Net.WebUtility.HtmlEncode(post.Hook))
                .Replace("{{Preview}}", System.Net.WebUtility.HtmlEncode(post.Hook + "\n\n" + post.Content[..Math.Min(post.Content.Length, 200)] + "..."))
                .Replace("{{ReviewUrl}}", reviewUrl)
                .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_email.FromName, _email.FromAddress));
            message.To.Add(MailboxAddress.Parse(user.Email));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_email.Host, _email.Port,
                _email.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
            if (!string.IsNullOrEmpty(_email.Username))
                await client.AuthenticateAsync(_email.Username, _email.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            log.Success = true;
            post.EmailedAt = DateTime.UtcNow;
            post.Status = "Emailed";
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", user.Email);
            log.Success = false;
            log.ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            _db.EmailLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

using NexaEmailBlast.Models;

namespace NexaEmailBlast.Services;

public static class EmailSenderFactory
{
    public static IEmailSender Create(AppConfig config)
    {
        var provider = (config.Sending.Provider ?? "graph").Trim().ToLowerInvariant();
        return provider switch
        {
            "smtp" => new SmtpEmailSender(config.Smtp, config.Sender),
            "graph" or "" => new GraphEmailSender(config.Graph, config.Sender),
            _ => throw new InvalidOperationException(
                $"Unknown Sending.Provider '{config.Sending.Provider}'. Use 'Graph' or 'Smtp'."),
        };
    }

    public static string Describe(AppConfig config)
    {
        var provider = (config.Sending.Provider ?? "graph").Trim().ToLowerInvariant();
        return provider switch
        {
            "smtp" => $"SMTP {config.Smtp.Host}:{config.Smtp.Port}",
            _ => $"Microsoft Graph ({config.Graph.Host}, tenant={Mask(config.Graph.TenantId)}, client={Mask(config.Graph.ClientId)})",
        };
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "<not set>";
        return value.Length <= 6 ? "******" : value[..4] + "…" + value[^2..];
    }
}

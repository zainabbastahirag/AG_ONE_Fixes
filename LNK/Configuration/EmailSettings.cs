namespace LNK.Configuration;

public class EmailSettings
{
    public const string SectionName = "Email";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromName { get; set; } = "LNK";
    public string FromAddress { get; set; } = "hello@lnk.app";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public string AppBaseUrl { get; set; } = "https://localhost:5001";
}

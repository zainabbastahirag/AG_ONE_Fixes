namespace NexaEmailBlast.Models;

public sealed class Recipient
{
    public string Email { get; set; } = "";
    public string? Name { get; set; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Email) && Email.Contains('@');
}

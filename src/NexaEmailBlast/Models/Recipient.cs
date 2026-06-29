namespace NexaEmailBlast.Models;

/// <summary>An image embedded inline in an email, referenced from the HTML via <c>cid:{ContentId}</c>.</summary>
public sealed record InlineImage(string ContentId, string Path, string ContentType = "image/png");

public sealed class Recipient
{
    public string Email { get; set; } = "";
    public string? Name { get; set; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Email) && Email.Contains('@');
}

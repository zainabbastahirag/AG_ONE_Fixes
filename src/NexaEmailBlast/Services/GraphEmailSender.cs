using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Users.Item.SendMail;
using NexaEmailBlast.Models;
using Graph = Microsoft.Graph.Models;

namespace NexaEmailBlast.Services;

/// <summary>
/// Sends mail through the Microsoft Graph API using app-only (client credentials) auth.
/// The Nexa image is attached as an inline fileAttachment referenced by the HTML via cid:nexa_sphere.
/// </summary>
public sealed class GraphEmailSender : IEmailSender
{
    private readonly GraphConfig _graph;
    private readonly SenderConfig _sender;
    private readonly GraphServiceClient _client;

    public GraphEmailSender(GraphConfig graph, SenderConfig sender)
    {
        _graph = graph;
        _sender = sender;

        if (string.IsNullOrWhiteSpace(graph.TenantId) ||
            string.IsNullOrWhiteSpace(graph.ClientId) ||
            string.IsNullOrWhiteSpace(graph.ClientSecret))
        {
            throw new InvalidOperationException(
                "Graph provider requires Graph.TenantId, Graph.ClientId and Graph.ClientSecret in appsettings.json " +
                "(or via NEXA_Graph__TenantId / NEXA_Graph__ClientId / NEXA_Graph__ClientSecret).");
        }

        var credential = new ClientSecretCredential(graph.TenantId, graph.ClientId, graph.ClientSecret);
        var scopes = new[] { string.IsNullOrWhiteSpace(graph.Scope) ? "https://graph.microsoft.com/.default" : graph.Scope };
        _client = new GraphServiceClient(credential, scopes, graph.BaseUrl);
    }

    public async Task SendAsync(
        Recipient recipient,
        string subject,
        string htmlBody,
        IReadOnlyList<InlineImage> inlineImages,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null)
    {
        var message = BuildMessage(recipient, subject, htmlBody, inlineImages, cc, bcc);
        var requestBody = new SendMailPostRequestBody
        {
            Message = message,
            SaveToSentItems = _graph.SaveToSentItems,
        };

        // App-only sendMail must target the mailbox explicitly (the Nexa sender address).
        await _client.Users[_sender.Email].SendMail.PostAsync(requestBody);
    }

    /// <summary>Builds the Graph <see cref="Graph.Message"/> (exposed for testing/serialization checks).</summary>
    public static Graph.Message BuildMessage(
        Recipient recipient,
        string subject,
        string htmlBody,
        IReadOnlyList<InlineImage> inlineImages,
        IEnumerable<string>? cc = null,
        IEnumerable<string>? bcc = null)
    {
        var message = new Graph.Message
        {
            Subject = subject,
            Body = new Graph.ItemBody { ContentType = Graph.BodyType.Html, Content = htmlBody },
            ToRecipients = new List<Graph.Recipient> { ToGraph(recipient.Email, recipient.Name) },
        };

        var ccList = ToGraphList(cc);
        if (ccList.Count > 0) message.CcRecipients = ccList;

        var bccList = ToGraphList(bcc);
        if (bccList.Count > 0) message.BccRecipients = bccList;

        var attachments = new List<Graph.Attachment>();
        foreach (var img in inlineImages)
        {
            if (!File.Exists(img.Path)) continue;
            attachments.Add(new Graph.FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = Path.GetFileName(img.Path),
                ContentType = img.ContentType,
                ContentBytes = File.ReadAllBytes(img.Path),
                ContentId = img.ContentId,
                IsInline = true,
            });
        }
        if (attachments.Count > 0) message.Attachments = attachments;

        return message;
    }

    /// <summary>Serializes the Graph request body to JSON (with the image bytes truncated) for API-style debugging.</summary>
    public static async Task<string> ToDebugJsonAsync(Graph.Message message)
    {
        Microsoft.Kiota.Abstractions.Serialization.SerializationWriterFactoryRegistry.DefaultInstance
            .ContentTypeAssociatedFactories["application/json"] =
            new Microsoft.Kiota.Serialization.Json.JsonSerializationWriterFactory();

        var body = new SendMailPostRequestBody { Message = message, SaveToSentItems = true };
        var json = await Microsoft.Kiota.Abstractions.Serialization.KiotaJsonSerializer.SerializeAsStringAsync(body);

        // Replace the huge base64 attachment payload with a short marker so the dump stays readable.
        return System.Text.RegularExpressions.Regex.Replace(
            json,
            "(\"contentBytes\":\")[^\"]+(\")",
            m => m.Groups[1].Value + "<base64 image bytes omitted>" + m.Groups[2].Value);
    }

    private static Graph.Recipient ToGraph(string address, string? name) => new()
    {
        EmailAddress = new Graph.EmailAddress
        {
            Address = address,
            Name = string.IsNullOrWhiteSpace(name) ? address : name,
        },
    };

    private static List<Graph.Recipient> ToGraphList(IEnumerable<string>? addresses)
    {
        var list = new List<Graph.Recipient>();
        if (addresses is null) return list;
        foreach (var a in addresses)
        {
            var trimmed = a.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Contains('@'))
                list.Add(ToGraph(trimmed, null));
        }
        return list;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

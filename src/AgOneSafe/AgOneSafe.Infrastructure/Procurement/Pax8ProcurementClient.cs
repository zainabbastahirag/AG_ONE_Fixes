using AgOneSafe.Application.Escalations;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Escalations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgOneSafe.Infrastructure.Procurement;

public sealed class Pax8Options
{
    public const string SectionName = "Pax8";

    /// <summary>When false the client records the quote locally instead of calling the distributor.</summary>
    public bool Enabled { get; set; }

    /// <summary>MCP endpoint exposing the Pax8 ordering tools.</summary>
    public string McpEndpoint { get; set; } = string.Empty;

    public string CompanyId { get; set; } = string.Empty;
}

/// <summary>
/// Routes licence and services requests to Pax8. The MVP records a local quote reference and the
/// full MCP round-trip is enabled by configuration, so the escalation workflow can be demonstrated
/// and tested before the distributor account exists.
/// </summary>
public sealed class Pax8ProcurementClient : IProcurementClient
{
    private readonly Pax8Options _options;
    private readonly ILogger<Pax8ProcurementClient> _logger;

    public Pax8ProcurementClient(IOptions<Pax8Options> options, ILogger<Pax8ProcurementClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<ProcurementResponse> SubmitAsync(
        ServiceEscalation escalation,
        CancellationToken cancellationToken = default)
    {
        var reference = $"PAX8-{DateTimeOffset.UtcNow:yyyyMMdd}-{escalation.Id:D5}";

        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Pax8 integration is disabled; recorded {Type} request {Reference} locally", escalation.Type, reference);

            return Task.FromResult(new ProcurementResponse(
                true,
                reference,
                escalation.Type == EscalationType.LicenseUpgrade
                    ? $"Quote {reference} raised for {escalation.SeatCount} x {escalation.RequestedSku}. Enable the Pax8 connector to submit it to the distributor automatically."
                    : $"Services request {reference} logged for the Aventra delivery team."));
        }

        // Live path: call the Pax8 MCP server's create-order tool with the tenant's company id.
        throw new NotSupportedException(
            "The Pax8 MCP connector is configured but not wired up in the sample. Implement the MCP call here.");
    }
}

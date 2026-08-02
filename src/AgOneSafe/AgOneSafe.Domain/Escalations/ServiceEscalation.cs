using System.ComponentModel.DataAnnotations;
using AgOneSafe.Domain.Assessments;
using AgOneSafe.Domain.Tenants;

namespace AgOneSafe.Domain.Escalations;

/// <summary>
/// Raised when a gap cannot be closed by a policy toggle - it needs a licence uplift
/// (routed to Pax8) or an Aventra professional-services engagement.
/// </summary>
public class ServiceEscalation
{
    public int Id { get; set; }

    public int TenantConnectionId { get; set; }
    public TenantConnection? TenantConnection { get; set; }

    public int? FindingId { get; set; }
    public Finding? Finding { get; set; }

    public EscalationType Type { get; set; }

    public EscalationStatus Status { get; set; } = EscalationStatus.Draft;

    /// <summary>SKU being requested, e.g. "Microsoft Entra ID P2".</summary>
    [MaxLength(128)]
    public string? RequestedSku { get; set; }

    public int SeatCount { get; set; }

    [MaxLength(2048)]
    public string Justification { get; set; } = string.Empty;

    /// <summary>Correlation id returned by the Pax8 MCP procurement call.</summary>
    [MaxLength(128)]
    public string? Pax8QuoteReference { get; set; }

    [MaxLength(128)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }

    [MaxLength(1024)]
    public string? ProviderMessage { get; set; }
}

using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Escalations;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Application.Escalations;

/// <summary>
/// Procurement side of the loop. In production this is the Pax8 MCP tool call; the interface keeps
/// the workflow testable and lets a distributor be swapped without touching the UI.
/// </summary>
public interface IProcurementClient
{
    Task<ProcurementResponse> SubmitAsync(ServiceEscalation escalation, CancellationToken cancellationToken = default);
}

public sealed record ProcurementResponse(bool Accepted, string Reference, string Message);

public interface IEscalationService
{
    Task<ServiceEscalation> RaiseAsync(EscalationRequest request, CancellationToken cancellationToken = default);
    Task<ServiceEscalation> SubmitAsync(int escalationId, CancellationToken cancellationToken = default);
}

public sealed class EscalationRequest
{
    public required int TenantConnectionId { get; init; }
    public int? FindingId { get; init; }
    public required EscalationType Type { get; init; }
    public string? RequestedSku { get; init; }
    public int SeatCount { get; init; } = 1;
    public string Justification { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}

public sealed class EscalationService : IEscalationService
{
    private readonly IAgOneSafeDbContext _db;
    private readonly IProcurementClient _procurement;
    private readonly IAuditTrail _audit;

    public EscalationService(IAgOneSafeDbContext db, IProcurementClient procurement, IAuditTrail audit)
    {
        _db = db;
        _procurement = procurement;
        _audit = audit;
    }

    public async Task<ServiceEscalation> RaiseAsync(EscalationRequest request, CancellationToken cancellationToken = default)
    {
        var escalation = new ServiceEscalation
        {
            TenantConnectionId = request.TenantConnectionId,
            FindingId = request.FindingId,
            Type = request.Type,
            RequestedSku = request.RequestedSku,
            SeatCount = request.SeatCount,
            Justification = request.Justification,
            RequestedBy = request.RequestedBy,
            Status = EscalationStatus.Draft
        };

        _db.ServiceEscalations.Add(escalation);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AuditEvent
        {
            EventType = AuditEventType.EscalationRaised,
            TenantConnectionId = request.TenantConnectionId,
            FindingId = request.FindingId,
            Actor = request.RequestedBy,
            ActorRole = "User",
            Summary = request.Type == EscalationType.LicenseUpgrade
                ? $"Licence upgrade requested: {request.RequestedSku} x{request.SeatCount}."
                : "Aventra professional-services engagement requested.",
            Details = new { request.RequestedSku, request.SeatCount, request.Justification }
        }, cancellationToken);

        return escalation;
    }

    public async Task<ServiceEscalation> SubmitAsync(int escalationId, CancellationToken cancellationToken = default)
    {
        var escalation = await _db.ServiceEscalations
                             .FirstOrDefaultAsync(e => e.Id == escalationId, cancellationToken)
                         ?? throw new InvalidOperationException($"Escalation {escalationId} not found.");

        var response = await _procurement.SubmitAsync(escalation, cancellationToken);

        escalation.Status = response.Accepted ? EscalationStatus.Submitted : EscalationStatus.Rejected;
        escalation.Pax8QuoteReference = response.Reference;
        escalation.ProviderMessage = response.Message;
        escalation.SubmittedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return escalation;
    }
}

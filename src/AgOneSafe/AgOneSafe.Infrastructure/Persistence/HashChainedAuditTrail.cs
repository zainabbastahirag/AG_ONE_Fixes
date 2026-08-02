using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Infrastructure.Persistence;

/// <summary>
/// Append-only audit store. Each row hashes its own content together with the previous row's hash,
/// so a deleted or edited record is detectable without trusting the database's own permissions.
/// In production the same rows are mirrored into the customer's Microsoft Purview audit log.
/// </summary>
public sealed class HashChainedAuditTrail : IAuditTrail
{
    private static readonly SemaphoreSlim ChainLock = new(1, 1);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AgOneSafeDbContext _db;

    public HashChainedAuditTrail(AgOneSafeDbContext db) => _db = db;

    public async Task<AuditLogEntry> RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        // The chain is only correct if entries are appended one at a time.
        await ChainLock.WaitAsync(cancellationToken);
        try
        {
            var previousHash = await _db.AuditLogEntries
                                   .OrderByDescending(e => e.Id)
                                   .Select(e => e.EntryHash)
                                   .FirstOrDefaultAsync(cancellationToken)
                               ?? string.Empty;

            var entry = new AuditLogEntry
            {
                TenantConnectionId = auditEvent.TenantConnectionId,
                EventType = auditEvent.EventType,
                ControlReference = auditEvent.ControlReference,
                FindingId = auditEvent.FindingId,
                RemediationTaskId = auditEvent.RemediationTaskId,
                AssessmentId = auditEvent.AssessmentId,
                Actor = auditEvent.Actor,
                ActorRole = auditEvent.ActorRole,
                Summary = Truncate(auditEvent.Summary, 1024),
                DetailsJson = auditEvent.Details is null
                    ? "{}"
                    : JsonSerializer.Serialize(auditEvent.Details, SerializerOptions),
                OccurredAt = DateTimeOffset.UtcNow,
                PreviousHash = previousHash
            };

            entry.EntryHash = ComputeHash(entry);

            _db.AuditLogEntries.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);

            return entry;
        }
        finally
        {
            ChainLock.Release();
        }
    }

    public async Task<(bool IsIntact, long? BrokenAtId)> VerifyChainAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _db.AuditLogEntries
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);

        var expectedPrevious = string.Empty;

        foreach (var entry in entries)
        {
            if (entry.PreviousHash != expectedPrevious || entry.EntryHash != ComputeHash(entry))
            {
                return (false, entry.Id);
            }

            expectedPrevious = entry.EntryHash;
        }

        return (true, null);
    }

    private static string ComputeHash(AuditLogEntry entry)
    {
        var canonical = string.Join('|',
            entry.PreviousHash,
            entry.TenantConnectionId?.ToString() ?? string.Empty,
            entry.EventType.ToString(),
            entry.ControlReference ?? string.Empty,
            entry.FindingId?.ToString() ?? string.Empty,
            entry.RemediationTaskId?.ToString() ?? string.Empty,
            entry.AssessmentId?.ToString() ?? string.Empty,
            entry.Actor,
            entry.ActorRole,
            entry.Summary,
            entry.DetailsJson,
            entry.OccurredAt.ToUniversalTime().ToString("O"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

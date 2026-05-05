using BabaPortal.Api.Data;
using BabaPortal.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BabaPortal.Api.Services;

/// Persistent + vector memory:
///  - stores facts/preferences/summaries with embeddings in SQLite
///  - retrieves top-K most similar memories for a query (cosine sim)
///  - stays out of server RAM: scoring is computed on a streamed query result
public class MemoryService
{
    private readonly BabaDbContext _db;
    private readonly EmbeddingService _embed;
    private readonly ILogger<MemoryService> _log;
    private static readonly System.Buffers.SearchValues<char> SentenceTerminators =
        System.Buffers.SearchValues.Create(".!?\n");

    public MemoryService(BabaDbContext db, EmbeddingService embed, ILogger<MemoryService> log)
    {
        _db = db; _embed = embed; _log = log;
    }

    public async Task<MemoryEntry> RememberAsync(Guid userId, string content, string kind = "fact", float importance = 0.5f, CancellationToken ct = default)
    {
        var emb = await _embed.EmbedAsync(content, ct);
        var entry = new MemoryEntry
        {
            UserId = userId,
            Content = content.Trim(),
            Kind = kind,
            Importance = Math.Clamp(importance, 0, 1),
            Embedding = EmbeddingService.ToBytes(emb),
            EmbeddingDim = emb.Length,
        };
        _db.Memories.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<List<MemoryEntry>> RecallAsync(Guid userId, string query, int topK = 6, CancellationToken ct = default)
    {
        var qVec = await _embed.EmbedAsync(query, ct);

        // Stream rows from SQLite without loading everything into memory at once.
        var scored = new List<(MemoryEntry e, float score)>();
        await foreach (var m in _db.Memories
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Importance)
            .ThenByDescending(m => m.LastUsedAt)
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            if (m.EmbeddingDim != qVec.Length) continue;
            var v = EmbeddingService.FromBytes(m.Embedding, m.EmbeddingDim);
            var sim = EmbeddingService.CosineSim(qVec, v);
            // mild boost for importance and recency
            var ageHours = (DateTime.UtcNow - m.LastUsedAt).TotalHours;
            var recency = (float)(1.0 / (1.0 + ageHours / 240.0));
            var score = sim * 0.8f + m.Importance * 0.1f + recency * 0.1f;
            scored.Add((m, score));
        }

        var top = scored.OrderByDescending(x => x.score).Take(topK).Select(x => x.e).ToList();

        // touch usage
        if (top.Count > 0)
        {
            var ids = top.Select(t => t.Id).ToList();
            await _db.Memories.Where(m => ids.Contains(m.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.LastUsedAt, _ => DateTime.UtcNow)
                    .SetProperty(m => m.UseCount, m => m.UseCount + 1), ct);
        }
        return top;
    }

    public async Task<List<MemoryEntry>> ListAsync(Guid userId, int limit = 200, CancellationToken ct = default)
    {
        return await _db.Memories.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, Guid memoryId, CancellationToken ct = default)
    {
        await _db.Memories.Where(m => m.UserId == userId && m.Id == memoryId).ExecuteDeleteAsync(ct);
    }

    /// Heuristic auto-extractor: pulls likely-facts from a user message.
    /// Lines like "I am ...", "my name is ...", "I like ...", "remember that ..." -> stored.
    public async Task AutoExtractAndStoreAsync(Guid userId, string userText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userText)) return;
        var lowered = userText.ToLowerInvariant();
        var facts = new List<(string text, string kind, float importance)>();

        string[] strongTriggers = { "remember that", "remember this", "don't forget", "do not forget", "note that" };
        foreach (var trig in strongTriggers)
        {
            var idx = lowered.IndexOf(trig, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var fact = userText[(idx + trig.Length)..].Trim(' ', ':', '-', '\u2014');
                if (fact.Length > 3) facts.Add((fact, "fact", 0.9f));
            }
        }

        string[] selfPatterns = { "my name is ", "i am ", "i'm ", "i live in ", "i work as ", "i work at ", "my favorite ", "i like ", "i love ", "i hate ", "my birthday is ", "i was born in " };
        foreach (var pat in selfPatterns)
        {
            int from = 0;
            while (true)
            {
                var idx = lowered.IndexOf(pat, from, StringComparison.Ordinal);
                if (idx < 0) break;
                var endRel = userText.AsSpan(idx).IndexOfAny(SentenceTerminators);
                var end = endRel < 0 ? userText.Length : idx + endRel;
                var fact = userText[idx..end].Trim();
                if (fact.Length > 4 && fact.Length < 280)
                    facts.Add((fact, pat.StartsWith("my ") || pat.Contains("name") ? "preference" : "preference", 0.7f));
                from = end + 1;
                if (from >= userText.Length) break;
            }
        }

        foreach (var (text, kind, imp) in facts.DistinctBy(f => f.text.ToLowerInvariant()))
        {
            try { await RememberAsync(userId, text, kind, imp, ct); }
            catch (Exception ex) { _log.LogDebug(ex, "remember failed"); }
        }
    }
}

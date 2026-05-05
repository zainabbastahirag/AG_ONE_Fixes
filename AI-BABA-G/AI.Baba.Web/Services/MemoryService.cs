using System.Buffers;
using System.Collections.Concurrent;
using AI.Baba.Web.Data;
using AI.Baba.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.Baba.Web.Services;

/// Unified memory service:
///  - Legacy guest memory: in-process, session-keyed, last-N history, used by /api/ask.
///  - Persistent + vector memory for authenticated users: SQLite-backed via EF Core
///    with cosine-similarity recall. Stored on disk to keep server RAM low.
///
/// IMPORTANT: registered as Scoped because it now depends on a scoped DbContext.
/// The legacy in-process memory is held in static state so it survives between scopes.
public class MemoryService
{
    // ─── Legacy guest memory (kept for /api/ask backwards compat) ─────────
    private static readonly ConcurrentDictionary<string, UserMemory> _guestMemories = new();
    private const int MaxHistory = 10;

    // ─── Persistent memory dependencies ───────────────────────────────────
    private readonly BabaDbContext _db;
    private readonly EmbeddingService _embed;
    private readonly ILogger<MemoryService> _log;

    private static readonly SearchValues<char> SentenceTerminators = SearchValues.Create(".!?\n");

    public MemoryService(BabaDbContext db, EmbeddingService embed, ILogger<MemoryService> log)
    {
        _db = db; _embed = embed; _log = log;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  LEGACY GUEST API (used by /api/ask)
    // ═════════════════════════════════════════════════════════════════════

    public UserMemory GetMemory(string sessionId)
        => _guestMemories.GetOrAdd(sessionId, _ => new UserMemory());

    public void AddToHistory(string sessionId, string role, string content)
    {
        var mem = GetMemory(sessionId);
        mem.History.Add(new ConversationEntry { Role = role, Content = content });
        if (mem.History.Count > MaxHistory)
            mem.History.RemoveRange(0, mem.History.Count - MaxHistory);
    }

    public void SetUserName(string sessionId, string name)
        => GetMemory(sessionId).Name = name;

    /// Builds the original mystical Baba-G system prompt.
    public string BuildContextPrompt(string sessionId, string avatar, string mindset)
    {
        var mem = GetMemory(sessionId);
        return BuildBaseSystemPrompt(avatar, mindset, mem.Name, BuildLegacyHistory(mem));
    }

    private static string BuildLegacyHistory(UserMemory mem)
    {
        if (mem.History.Count == 0) return string.Empty;
        var recent = mem.History.TakeLast(6);
        return "Recent conversation:\n" + string.Join("\n", recent.Select(h => $"{h.Role}: {h.Content}"));
    }

    public static string BuildBaseSystemPrompt(string avatar, string mindset, string? userName, string historyContext)
    {
        var avatarPersonality = (avatar ?? "sage").ToLowerInvariant() switch
        {
            "sage" => "You are The Sage — ancient, deeply wise, calm, and insightful. You speak with gravitas and timeless wisdom.",
            "philosopher" => "You are The Philosopher — analytical, deep-thinking, Socratic. You question assumptions and explore ideas.",
            "healer" => "You are The Healer — compassionate, gentle, empathetic. You focus on emotional well-being and inner peace.",
            "elder" => "You are The Elder — experienced, traditional, grounded. You share practical life wisdom from decades of living.",
            "storyteller" => "You are The Storyteller — creative, engaging, narrative-driven. You teach through parables and vivid stories.",
            "designer" => "You are BABA-G as The Designer — a senior product/UI-UX designer. Talk about layout, hierarchy, color, typography, motion, accessibility, and brand. Be specific and actionable.",
            "developer" => "You are BABA-G as The Developer — a senior full-stack engineer. Be precise about code, architecture, trade-offs, and best practices. Use code blocks only when asked to write code.",
            "pm" => "You are BABA-G as The Project Manager — pragmatic, organized, outcome-oriented. Help with scope, prioritization, sprints, risks, and stakeholder communication.",
            "marketing" => "You are BABA-G as The Marketing strategist — clear copy, growth channels, positioning, audience, funnel. Be punchy and specific.",
            "sales" => "You are BABA-G as The Sales coach — discovery, pitch, objection handling, closing. Friendly, confident, never pushy.",
            "hr" => "You are BABA-G as The HR partner — empathetic and policy-aware. Help with hiring, culture, performance, and people problems with care.",
            _ => "You are a wise AI guide."
        };

        var mindsetTone = (mindset ?? "balanced").ToLowerInvariant() switch
        {
            "balanced" => "Give well-rounded, fair perspectives for all situations.",
            "logical" => "Be clear, rational, and practical. Use evidence-based reasoning.",
            "spiritual" => "Be soulful, mindful, and focused on inner growth and consciousness.",
            "motivational" => "Be encouraging, uplifting, and empowering. Inspire action.",
            "creative" => "Be innovative, out-of-the-box, imaginative. Suggest unexpected approaches.",
            _ => "Be balanced and helpful."
        };

        var nameContext = !string.IsNullOrEmpty(userName)
            ? $"The user's name is {userName}. Use their name naturally."
            : string.Empty;

        return $@"You are AI Baba-G, a wise, slightly humorous, deeply intelligent voice assistant.
{avatarPersonality}
{mindsetTone}
{nameContext}

VOICE STYLE:
- Reply in 1 to 3 short sentences by default. End each sentence with a period, question mark, or exclamation so a TTS engine pauses naturally.
- Never produce walls of text. If a topic is complex, give the headline, then ask if the user wants details.
- Do NOT use markdown, bullet points, or code blocks unless the user explicitly asks for them.
- Speak like a real person, not a teleprompter — contractions, gentle pauses, simple words.
- Do NOT repeat the user's question back. Do NOT add filler like ""sure thing!"" or ""of course!"".
- Stop when you've answered. Do NOT keep going.

If the user tells you their name, remember it and use it warmly. Adapt your tone to the chosen mindset.

{historyContext}".Trim();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  PERSISTENT + VECTOR MEMORY (for authenticated users)
    // ═════════════════════════════════════════════════════════════════════

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
            var ageHours = (DateTime.UtcNow - m.LastUsedAt).TotalHours;
            var recency = (float)(1.0 / (1.0 + ageHours / 240.0));
            var score = sim * 0.8f + m.Importance * 0.1f + recency * 0.1f;
            scored.Add((m, score));
        }

        var top = scored.OrderByDescending(x => x.score).Take(topK).Select(x => x.e).ToList();

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

    /// Heuristic auto-extractor: pulls likely-facts from a user message and persists them.
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

        string[] selfPatterns =
        {
            "my name is ", "i am ", "i'm ",
            "i live in ", "i work as ", "i work at ",
            "my favorite ", "i like ", "i love ", "i hate ",
            "my birthday is ", "i was born in "
        };
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
                    facts.Add((fact, "preference", 0.7f));
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

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BabaPortal.Api.Data;
using BabaPortal.Api.Dtos;
using BabaPortal.Api.Models;
using BabaPortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BabaPortal.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly BabaDbContext _db;
    private readonly OllamaService _ollama;
    private readonly MemoryService _memory;
    private readonly ILogger<ChatController> _log;

    public ChatController(BabaDbContext db, OllamaService ollama, MemoryService memory, ILogger<ChatController> log)
    {
        _db = db; _ollama = ollama; _memory = memory; _log = log;
    }

    /// Server-Sent Events streaming endpoint for *guests* (no memory, no history).
    [HttpPost("guest/stream")]
    public async Task GuestStream([FromBody] GuestChatRequest req, CancellationToken ct)
    {
        await PrepareSseAsync();
        var sysPrompt = await ResolvePersonalityPromptAsync(req.PersonalityId, ct)
            ?? "You are BABA, a witty fun-portal AI. You have NO memory of this user because they aren't signed in. Reply in 1-3 short sentences. If the user asks personal questions, gently invite them to sign up so you can remember them.";
        var msgs = new List<OllamaService.OllamaMessage>
        {
            new("system", sysPrompt),
            new("user", req.Message)
        };
        await StreamToClientAsync(msgs, conversationId: null, persistAssistantToConversationId: null, ct);
    }

    /// Authenticated streaming chat with persistent + vector memory.
    [Authorize]
    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequest req, CancellationToken ct)
    {
        await PrepareSseAsync();
        var userId = GetUserId();
        if (userId is null) { Response.StatusCode = StatusCodes.Status401Unauthorized; return; }

        // 1) load or create conversation
        Conversation conv;
        if (req.ConversationId is Guid cid)
        {
            conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == cid && c.UserId == userId, ct)
                ?? throw new InvalidOperationException("Conversation not found.");
        }
        else
        {
            conv = new Conversation
            {
                UserId = userId.Value,
                PersonalityId = req.PersonalityId,
                Title = req.Message.Length > 48 ? req.Message[..48] + "…" : req.Message,
            };
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync(ct);
            await SendSseAsync("meta", JsonSerializer.Serialize(new { conversationId = conv.Id, title = conv.Title }));
        }

        // 2) save user message
        var userMsg = new Message { ConversationId = conv.Id, Role = "user", Content = req.Message };
        _db.Messages.Add(userMsg);
        conv.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // 3) extract any "remember X" facts in the background
        _ = Task.Run(async () =>
        {
            try { await _memory.AutoExtractAndStoreAsync(userId.Value, req.Message); }
            catch (Exception ex) { _log.LogDebug(ex, "auto extract failed"); }
        }, CancellationToken.None);

        // 4) recall top memories using vector sim
        var recalled = await _memory.RecallAsync(userId.Value, req.Message, topK: 6, ct);
        var memBlock = recalled.Count == 0 ? string.Empty :
            "RELEVANT LONG-TERM MEMORY ABOUT THE USER (use naturally, don't list it):\n" +
            string.Join('\n', recalled.Select(r => $"- ({r.Kind}) {r.Content}"));

        // 5) load short-term recent history (last 16)
        var history = await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conv.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(16)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var personalityPrompt = await ResolvePersonalityPromptAsync(req.PersonalityId ?? conv.PersonalityId, ct)
            ?? "You are BABA, a memory-aware fun AI companion. You remember the user across sessions, adapt to their personality, keep replies tight (1-4 short sentences) for fast voice playback. Avoid lists and markdown unless the user asks. Speak naturally like a real friend.";

        var sb = new StringBuilder();
        sb.AppendLine(personalityPrompt);
        sb.AppendLine();
        sb.AppendLine("Style rules: Keep replies concise so they feel real-time when spoken aloud. Use plain text. Don't restate memories verbatim - weave them in.");
        if (memBlock.Length > 0) { sb.AppendLine(); sb.AppendLine(memBlock); }

        var msgs = new List<OllamaService.OllamaMessage> { new("system", sb.ToString()) };
        foreach (var m in history) msgs.Add(new(m.Role, m.Content));

        await StreamToClientAsync(msgs, conversationId: conv.Id, persistAssistantToConversationId: conv.Id, ct);
    }

    private async Task<string?> ResolvePersonalityPromptAsync(Guid? id, CancellationToken ct)
    {
        if (id is null) return null;
        var p = await _db.Personalities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p?.SystemPrompt;
    }

    private async Task StreamToClientAsync(
        List<OllamaService.OllamaMessage> msgs,
        Guid? conversationId,
        Guid? persistAssistantToConversationId,
        CancellationToken ct)
    {
        var full = new StringBuilder();
        try
        {
            await foreach (var token in _ollama.StreamChatAsync(msgs, ct: ct))
            {
                full.Append(token);
                await SendSseAsync("token", token);
            }
        }
        catch (OperationCanceledException) { /* client disconnected / interrupted */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "stream error");
            await SendSseAsync("error", ex.Message);
        }
        finally
        {
            if (persistAssistantToConversationId is Guid cid && full.Length > 0)
            {
                try
                {
                    _db.Messages.Add(new Message { ConversationId = cid, Role = "assistant", Content = full.ToString() });
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception ex) { _log.LogDebug(ex, "persist assistant message failed"); }
            }
            await SendSseAsync("done", "1");
        }
    }

    private async Task PrepareSseAsync()
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");
        await Response.Body.FlushAsync();
    }

    private async Task SendSseAsync(string @event, string data)
    {
        var payload = $"event: {@event}\ndata: {data.Replace("\r", "").Replace("\n", "\\n")}\n\n";
        var bytes = Encoding.UTF8.GetBytes(payload);
        await Response.Body.WriteAsync(bytes);
        await Response.Body.FlushAsync();
    }

    private Guid? GetUserId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(s, out var g) ? g : null;
    }

    [Authorize]
    [HttpGet("conversations")]
    public async Task<IActionResult> ListConversations(CancellationToken ct)
    {
        var uid = GetUserId(); if (uid is null) return Unauthorized();
        var list = await _db.Conversations.AsNoTracking()
            .Where(c => c.UserId == uid)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new { c.Id, c.Title, c.UpdatedAt, c.PersonalityId })
            .Take(100)
            .ToListAsync(ct);
        return Ok(list);
    }

    [Authorize]
    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> GetConversation(Guid id, CancellationToken ct)
    {
        var uid = GetUserId(); if (uid is null) return Unauthorized();
        var c = await _db.Conversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct);
        if (c is null) return NotFound();
        var msgs = await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Id, m.Role, m.Content, m.CreatedAt })
            .ToListAsync(ct);
        return Ok(new { c.Id, c.Title, c.PersonalityId, messages = msgs });
    }

    [Authorize]
    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid id, CancellationToken ct)
    {
        var uid = GetUserId(); if (uid is null) return Unauthorized();
        await _db.Messages.Where(m => m.Conversation!.UserId == uid && m.ConversationId == id).ExecuteDeleteAsync(ct);
        await _db.Conversations.Where(c => c.UserId == uid && c.Id == id).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}

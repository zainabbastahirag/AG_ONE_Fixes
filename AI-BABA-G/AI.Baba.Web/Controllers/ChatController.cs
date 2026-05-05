using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AI.Baba.Web.Data;
using AI.Baba.Web.Models;
using AI.Baba.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI.Baba.Web.Controllers;

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

    /// SSE streaming endpoint for *guests* (no persistent memory, no history).
    /// Uses the mystical Baba-G prompt with the chosen avatar/mindset.
    [HttpPost("guest/stream")]
    public async Task GuestStream([FromBody] GuestStreamChatRequest req, CancellationToken ct)
    {
        await PrepareSseAsync();
        var sysPrompt = await ResolvePersonalityPromptAsync(req.PersonalityId, req.Avatar, req.Mindset, userName: null, history: string.Empty, ct);
        sysPrompt += "\n\nNOTE: this user is a guest and is NOT signed in, so you have no long-term memory of them. Gently invite them to sign up if they ask you to remember anything.";

        var msgs = new List<OllamaService.ChatTurn>
        {
            new("system", sysPrompt),
            new("user", req.Message)
        };
        await StreamToClientAsync(msgs, persistAssistantToConversationId: null, ct);
    }

    /// Authenticated streaming chat with persistent + vector memory.
    [Authorize]
    [HttpPost("stream")]
    public async Task Stream([FromBody] StreamChatRequest req, CancellationToken ct)
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
            // update avatar/mindset selection if user changed them mid-conversation
            if (!string.IsNullOrWhiteSpace(req.Avatar)) conv.AvatarKey = req.Avatar;
            if (!string.IsNullOrWhiteSpace(req.Mindset)) conv.MindsetKey = req.Mindset;
            conv.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            conv = new Conversation
            {
                UserId = userId.Value,
                PersonalityId = req.PersonalityId,
                AvatarKey = req.Avatar,
                MindsetKey = req.Mindset,
                Title = req.Message.Length > 48 ? req.Message[..48] + "…" : req.Message,
            };
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync(ct);
            await SendSseAsync("meta", JsonSerializer.Serialize(new { conversationId = conv.Id, title = conv.Title }));
        }

        // 2) save user message
        var userMsg = new ChatMessage { ConversationId = conv.Id, Role = "user", Content = req.Message };
        _db.Messages.Add(userMsg);
        conv.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // 3) extract any "remember X" facts in the background
        _ = Task.Run(async () =>
        {
            try { await _memory.AutoExtractAndStoreAsync(userId.Value, req.Message); }
            catch (Exception ex) { _log.LogDebug(ex, "auto extract failed"); }
        }, CancellationToken.None);

        // 4) recall top memories using vector similarity
        var recalled = await _memory.RecallAsync(userId.Value, req.Message, topK: 6, ct);
        var memBlock = recalled.Count == 0 ? string.Empty :
            "RELEVANT LONG-TERM MEMORY ABOUT THE USER (use naturally, don't list it):\n" +
            string.Join('\n', recalled.Select(r => $"- ({r.Kind}) {r.Content}"));

        // 5) load short-term recent history (last 16) — also build a concise text for the prompt
        var history = await _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conv.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(16)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        var historyText = string.Empty; // history is sent as message turns; keep prompt slim
        var systemPrompt = await ResolvePersonalityPromptAsync(
            req.PersonalityId ?? conv.PersonalityId,
            req.Avatar ?? conv.AvatarKey,
            req.Mindset ?? conv.MindsetKey,
            userName: user?.DisplayName ?? user?.Username,
            history: historyText,
            ct);

        if (memBlock.Length > 0) systemPrompt += "\n\n" + memBlock;

        var msgs = new List<OllamaService.ChatTurn> { new("system", systemPrompt) };
        foreach (var m in history) msgs.Add(new(m.Role, m.Content));

        await StreamToClientAsync(msgs, persistAssistantToConversationId: conv.Id, ct);
    }

    private async Task<string> ResolvePersonalityPromptAsync(Guid? personalityId, string? avatar, string? mindset, string? userName, string history, CancellationToken ct)
    {
        // Custom personality wins if specified
        if (personalityId is Guid pid)
        {
            var p = await _db.Personalities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct);
            if (p is not null && !string.IsNullOrWhiteSpace(p.SystemPrompt))
            {
                var nameLine = string.IsNullOrEmpty(userName) ? "" : $"\nThe user's name is {userName}. Use it warmly.";
                return $"{p.SystemPrompt}\n\nKeep responses 1-4 short sentences for fast voice playback. Avoid markdown.{nameLine}";
            }
        }
        return MemoryService.BuildBaseSystemPrompt(avatar ?? "sage", mindset ?? "balanced", userName, history);
    }

    private async Task StreamToClientAsync(
        List<OllamaService.ChatTurn> msgs,
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
                    _db.Messages.Add(new ChatMessage { ConversationId = cid, Role = "assistant", Content = full.ToString() });
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
            .Select(c => new { c.Id, c.Title, c.UpdatedAt, c.PersonalityId, c.AvatarKey, c.MindsetKey })
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
        return Ok(new { c.Id, c.Title, c.PersonalityId, c.AvatarKey, c.MindsetKey, messages = msgs });
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

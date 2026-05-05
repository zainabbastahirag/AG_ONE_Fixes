using System.Security.Claims;
using AI.Baba.Web.Models;
using AI.Baba.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace AI.Baba.Web.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly OllamaService _ollama;
    private readonly MemoryService _memory;

    public ApiController(OllamaService ollama, MemoryService memory)
    {
        _ollama = ollama;
        _memory = memory;
    }

    /// Legacy non-streaming Ask endpoint — kept for backward compatibility.
    /// If the caller is authenticated, the prompt is enriched with vector-recalled long-term memory.
    [HttpPost("ask")]
    public async Task<ActionResult<AskResponse>> Ask([FromBody] AskRequest request, CancellationToken ct)
    {
        var sessionId = HttpContext.Session.Id;

        if (!string.IsNullOrWhiteSpace(request.UserName))
            _memory.SetUserName(sessionId, request.UserName);

        // Detect name introduction (legacy heuristic)
        var lower = request.Prompt.ToLower();
        if ((lower.Contains("my name is") || lower.Contains("i'm ") || lower.Contains("i am "))
            && string.IsNullOrEmpty(_memory.GetMemory(sessionId).Name))
        {
            var words = request.Prompt.Split(' ');
            var nameIdx = -1;
            for (int i = 0; i < words.Length - 1; i++)
            {
                if ((words[i].Equals("is", StringComparison.OrdinalIgnoreCase) ||
                     words[i].Equals("i'm", StringComparison.OrdinalIgnoreCase)) && i + 1 < words.Length)
                {
                    nameIdx = i + 1;
                    break;
                }
            }
            if (nameIdx > 0 && nameIdx < words.Length)
                _memory.SetUserName(sessionId, words[nameIdx].TrimEnd('.', ',', '!'));
        }

        _memory.AddToHistory(sessionId, "User", request.Prompt);

        var systemPrompt = _memory.BuildContextPrompt(sessionId, request.Avatar, request.Mindset);

        // Auth-aware enrichment: pull top-K vector memories and persist conversation if signed in.
        if (TryGetUserId(out var uid))
        {
            try
            {
                var recalled = await _memory.RecallAsync(uid, request.Prompt, topK: 6, ct);
                if (recalled.Count > 0)
                {
                    var memBlock = "RELEVANT LONG-TERM MEMORY ABOUT THE USER (use naturally, don't list it):\n" +
                        string.Join('\n', recalled.Select(r => $"- ({r.Kind}) {r.Content}"));
                    systemPrompt += "\n\n" + memBlock;
                }
                _ = Task.Run(async () =>
                {
                    try { await _memory.AutoExtractAndStoreAsync(uid, request.Prompt); }
                    catch { /* best-effort */ }
                }, CancellationToken.None);
            }
            catch { /* never fail Ask on memory issues */ }
        }

        var response = await _ollama.GenerateAsync(systemPrompt, request.Prompt, ct);

        _memory.AddToHistory(sessionId, "Baba", response);

        return Ok(new AskResponse
        {
            Success = true,
            Response = response,
            Avatar = request.Avatar,
            Mindset = request.Mindset
        });
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", app = "AI-BABA-G", version = "2.0" });

    [HttpGet("config")]
    public IActionResult Config([FromServices] IConfiguration cfg) => Ok(new
    {
        name = "AI BABA-G",
        chatModel = cfg["Ollama:ChatModel"] ?? cfg["Ollama:Model"] ?? "llama3.2",
        embeddingModel = cfg["Ollama:EmbeddingModel"] ?? "nomic-embed-text",
        ollamaBase = cfg["Ollama:BaseUrl"] ?? "http://localhost:11434"
    });

    private bool TryGetUserId(out Guid id)
    {
        id = Guid.Empty;
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(s, out id);
    }
}

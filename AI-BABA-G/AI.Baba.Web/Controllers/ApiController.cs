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

    [HttpPost("ask")]
    public async Task<ActionResult<AskResponse>> Ask([FromBody] AskRequest request, CancellationToken ct)
    {
        var sessionId = HttpContext.Session.Id;

        if (!string.IsNullOrWhiteSpace(request.UserName))
            _memory.SetUserName(sessionId, request.UserName);

        // Detect name introduction
        var lower = request.Prompt.ToLower();
        if ((lower.Contains("my name is") || lower.Contains("i'm ") || lower.Contains("i am ")) && string.IsNullOrEmpty(_memory.GetMemory(sessionId).Name))
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
    public IActionResult Health() => Ok(new { status = "ok", app = "AI-BABA-G", version = "1.0" });
}

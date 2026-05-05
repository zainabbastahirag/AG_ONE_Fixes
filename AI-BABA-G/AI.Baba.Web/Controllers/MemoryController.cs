using System.Security.Claims;
using AI.Baba.Web.Models;
using AI.Baba.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.Baba.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/memory")]
public class MemoryController : ControllerBase
{
    private readonly MemoryService _memory;
    public MemoryController(MemoryService memory) { _memory = memory; }

    private Guid Uid() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _memory.ListAsync(Uid(), 200, ct);
        return Ok(items.Select(m => new { m.Id, m.Content, m.Kind, m.Importance, m.CreatedAt, m.LastUsedAt, m.UseCount }));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddMemoryRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Content)) return BadRequest(new { error = "Content required" });
        var m = await _memory.RememberAsync(Uid(), r.Content, r.Kind ?? "fact", r.Importance ?? 0.6f, ct);
        return Ok(new { m.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _memory.DeleteAsync(Uid(), id, ct);
        return NoContent();
    }

    [HttpGet("recall")]
    public async Task<IActionResult> Recall([FromQuery] string q, CancellationToken ct)
    {
        var items = await _memory.RecallAsync(Uid(), q ?? "", 10, ct);
        return Ok(items.Select(m => new { m.Id, m.Content, m.Kind, m.Importance }));
    }
}

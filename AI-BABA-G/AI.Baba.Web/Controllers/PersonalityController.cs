using System.Security.Claims;
using AI.Baba.Web.Data;
using AI.Baba.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI.Baba.Web.Controllers;

[ApiController]
[Route("api/personalities")]
public class PersonalityController : ControllerBase
{
    private readonly BabaDbContext _db;
    public PersonalityController(BabaDbContext db) { _db = db; }

    private Guid? Uid() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : null;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var uid = Uid();
        var q = _db.Personalities.AsNoTracking();
        var items = uid is null
            ? await q.Where(p => p.UserId == null || p.IsPublic).ToListAsync(ct)
            : await q.Where(p => p.UserId == null || p.UserId == uid || p.IsPublic).ToListAsync(ct);
        return Ok(items.Select(p => new
        {
            p.Id,
            p.Name,
            p.Tagline,
            p.Voice,
            p.AvatarUrl,
            p.AvatarKey,
            p.MindsetKey,
            p.IsPublic,
            mine = uid.HasValue && p.UserId == uid,
            preset = p.UserId == null
        }));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePersonalityRequest r, CancellationToken ct)
    {
        var uid = Uid()!.Value;
        if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.SystemPrompt))
            return BadRequest(new { error = "Name and SystemPrompt required." });
        var p = new Personality
        {
            UserId = uid,
            Name = r.Name,
            Tagline = r.Tagline,
            SystemPrompt = r.SystemPrompt,
            Voice = string.IsNullOrWhiteSpace(r.Voice) ? "default" : r.Voice!,
            AvatarUrl = r.AvatarUrl,
            AvatarKey = r.AvatarKey,
            MindsetKey = r.MindsetKey,
            IsPublic = r.IsPublic,
        };
        _db.Personalities.Add(p);
        await _db.SaveChangesAsync(ct);
        return Ok(new { p.Id });
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var uid = Uid();
        await _db.Personalities.Where(p => p.Id == id && p.UserId == uid).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}

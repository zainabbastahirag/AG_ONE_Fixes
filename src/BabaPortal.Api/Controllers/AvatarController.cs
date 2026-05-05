using System.Security.Claims;
using BabaPortal.Api.Data;
using BabaPortal.Api.Dtos;
using BabaPortal.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BabaPortal.Api.Controllers;

[ApiController]
[Route("api/avatars")]
public class AvatarController : ControllerBase
{
    private readonly BabaDbContext _db;
    public AvatarController(BabaDbContext db) { _db = db; }
    private Guid? Uid() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : null;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var uid = Uid();
        var items = await _db.Avatars.AsNoTracking()
            .Where(a => a.UserId == null || a.IsPublic || a.UserId == uid)
            .ToListAsync(ct);
        return Ok(items.Select(a => new {
            a.Id, a.Name, a.Kind, a.ImageUrl, a.ModelUrl, a.PrimaryColor, a.IsPublic,
            mine = uid.HasValue && a.UserId == uid,
            preset = a.UserId == null
        }));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAvatarRequest r, CancellationToken ct)
    {
        var uid = Uid()!.Value;
        if (string.IsNullOrWhiteSpace(r.Name)) return BadRequest(new { error = "Name required." });
        var a = new Avatar
        {
            UserId = uid,
            Name = r.Name,
            Kind = string.IsNullOrWhiteSpace(r.Kind) ? "robot" : r.Kind!,
            ImageUrl = r.ImageUrl,
            ModelUrl = r.ModelUrl,
            PrimaryColor = string.IsNullOrWhiteSpace(r.PrimaryColor) ? "#7c3aed" : r.PrimaryColor!,
            IsPublic = r.IsPublic,
        };
        _db.Avatars.Add(a);
        await _db.SaveChangesAsync(ct);
        return Ok(new { a.Id });
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var uid = Uid();
        await _db.Avatars.Where(a => a.Id == id && a.UserId == uid).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}

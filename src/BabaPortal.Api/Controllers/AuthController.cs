using BabaPortal.Api.Dtos;
using BabaPortal.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BabaPortal.Api.Data;
using BabaPortal.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BabaPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly BabaDbContext _db;
    public AuthController(AuthService auth, BabaDbContext db) { _auth = auth; _db = db; }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest r, CancellationToken ct)
    {
        try
        {
            var (u, token) = await _auth.RegisterAsync(r.Username, r.Email, r.Password, r.DisplayName, ct);
            return Ok(new AuthResponse(token, ToDto(u)));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest r, CancellationToken ct)
    {
        try
        {
            var (u, token) = await _auth.LoginAsync(r.UsernameOrEmail, r.Password, ct);
            return Ok(new AuthResponse(token, ToDto(u)));
        }
        catch (InvalidOperationException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idStr, out var id)) return Unauthorized();
        var u = await _db.Users.FindAsync(new object?[] { id }, ct);
        return u is null ? NotFound() : Ok(ToDto(u));
    }

    private static UserDto ToDto(User u) => new(u.Id, u.Username, u.DisplayName, u.Email, u.AvatarUrl);
}

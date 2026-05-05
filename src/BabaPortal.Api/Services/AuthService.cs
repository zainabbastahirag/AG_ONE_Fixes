using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BabaPortal.Api.Data;
using BabaPortal.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BabaPortal.Api.Services;

public class AuthService
{
    private readonly BabaDbContext _db;
    private readonly IConfiguration _cfg;

    public AuthService(BabaDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    public async Task<(User user, string token)> RegisterAsync(string username, string email, string password, string? displayName, CancellationToken ct = default)
    {
        username = username.Trim();
        email = email.Trim().ToLowerInvariant();
        if (username.Length < 2 || username.Length > 64) throw new InvalidOperationException("Username must be 2-64 chars.");
        if (!email.Contains('@')) throw new InvalidOperationException("Invalid email.");
        if (password.Length < 6) throw new InvalidOperationException("Password too short (min 6).");

        if (await _db.Users.AnyAsync(u => u.Username == username, ct))
            throw new InvalidOperationException("Username already taken.");
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Username = username,
            Email = email,
            DisplayName = displayName ?? username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return (user, IssueToken(user));
    }

    public async Task<(User user, string token)> LoginAsync(string usernameOrEmail, string password, CancellationToken ct = default)
    {
        usernameOrEmail = usernameOrEmail.Trim();
        var key = usernameOrEmail.ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Username == usernameOrEmail || u.Email == key, ct)
            ?? throw new InvalidOperationException("Invalid credentials.");
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new InvalidOperationException("Invalid credentials.");
        user.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (user, IssueToken(user));
    }

    public string IssueToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!);
        var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
            },
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

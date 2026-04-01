using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AGOne.Authorization.Constants;
using AGOne.Authorization.Models;
using Microsoft.IdentityModel.Tokens;

namespace AGOne.Authorization.Services;

/// <summary>
/// Called by AG ONE gateway at login time to issue JWT tokens containing permissions.
/// Downstream apps validate this token and read permissions from claims.
/// </summary>
public interface IJwtPermissionTokenService
{
    string GenerateToken(Guid userId, Guid tenantId, string email, IEnumerable<string> roles, UserPermissionSet permissions, TimeSpan? expiry = null);
}

public sealed class JwtPermissionTokenService : IJwtPermissionTokenService
{
    private readonly AGOneAuthOptions _options;

    public JwtPermissionTokenService(AGOneAuthOptions options)
    {
        _options = options;
    }

    public string GenerateToken(
        Guid userId,
        Guid tenantId,
        string email,
        IEnumerable<string> roles,
        UserPermissionSet permissions,
        TimeSpan? expiry = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", tenantId.ToString()),
            new(Permissions.PermissionVersionClaim, permissions.Version.ToString()),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var perm in permissions.Permissions)
            claims.Add(new Claim(Permissions.ClaimType, perm));

        var token = new JwtSecurityToken(
            issuer: _options.JwtIssuer,
            audience: _options.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry ?? TimeSpan.FromHours(8)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using System.Security.Claims;
using AGOne.Authorization.Constants;
using Microsoft.AspNetCore.Http;

namespace AGOne.Authorization.Services;

public sealed class HttpContextUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private IReadOnlySet<string>? _cachedPermissions;

    public HttpContextUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public Guid? UserId
    {
        get
        {
            var sub = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User?.FindFirst("sub")?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var tid = User?.FindFirst("tenant_id")?.Value
                   ?? User?.FindFirst("tid")?.Value;
            return Guid.TryParse(tid, out var id) ? id : null;
        }
    }

    public long? PermissionVersion
    {
        get
        {
            var v = User?.FindFirst(Permissions.PermissionVersionClaim)?.Value;
            return long.TryParse(v, out var ver) ? ver : null;
        }
    }

    public IReadOnlySet<string> JwtPermissions
    {
        get
        {
            if (_cachedPermissions != null) return _cachedPermissions;

            var perms = User?.FindAll(Permissions.ClaimType)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _cachedPermissions = perms;
            return perms;
        }
    }
}

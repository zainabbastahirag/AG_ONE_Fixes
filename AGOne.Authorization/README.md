# AGOne.Authorization — Shared Permission Service

Shared .NET 8 + EF Core authorization library for the AG ONE product family.  
Inject into **any** product (AG ONE, Work, Learn, Safe, Pulse, Spot) to control access.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    AG ONE (Gateway)                       │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐  │
│  │ EF Core DB   │  │ JWT Issuer   │  │ GatewayPerm    │  │
│  │ (Roles,      │  │ (embeds      │  │ Service (reads │  │
│  │  Permissions, │  │  permissions │  │  from DB)      │  │
│  │  UserRoles)  │  │  + version)  │  │                │  │
│  └──────┬───────┘  └──────┬───────┘  └────────────────┘  │
│         │                 │                               │
│         │    JWT with permissions claim + perm_version    │
│         │                 │                               │
└─────────┼─────────────────┼───────────────────────────────┘
          │                 │  (cookie: agone_token)
          │                 ▼
   ┌──────┴──────────────────────────────────────────────┐
   │           Downstream Products                        │
   │   ┌──────────┐ ┌──────────┐ ┌──────────┐           │
   │   │ AG ONE   │ │ AG ONE   │ │ AG ONE   │  ...      │
   │   │ Work     │ │ Learn    │ │ Safe     │           │
   │   └────┬─────┘ └────┬─────┘ └────┬─────┘           │
   │        │             │             │                 │
   │   DownstreamPermissionService:                      │
   │   1. Read perms from JWT claims                     │
   │   2. Cache in IMemoryCache                          │
   │   3. If perm_version > cached → refresh from GW     │
   └─────────────────────────────────────────────────────┘
```

## How New Permissions Propagate

1. Admin changes a role in AG ONE → `InvalidateUserPermissionsAsync()` bumps `perm_version` in DB
2. Next token refresh/re-issue includes the new `perm_version` + updated permission claims
3. Downstream middleware (`PermissionRefreshMiddleware`) detects version mismatch → invalidates cache
4. Next permission check loads fresh data from JWT (or fetches from gateway API as fallback)

## Setup

### AG ONE Gateway (Program.cs)

```csharp
using AGOne.Authorization.Extensions;

builder.Services.AddAGOneGatewayAuthorization(
    options =>
    {
        options.JwtSecret = builder.Configuration["Jwt:Secret"]!;
        options.JwtIssuer = "agone";
        options.JwtAudience = "agone-products";
        options.CacheDuration = TimeSpan.FromMinutes(5);
    },
    db => db.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
```

### Downstream Product — e.g. AG ONE Work (Program.cs)

```csharp
using AGOne.Authorization.Extensions;

builder.Services.AddAGOneProductAuthorization(options =>
{
    options.GatewayBaseUrl = builder.Configuration["AGOne:GatewayUrl"]!;
    options.JwtSecret = builder.Configuration["Jwt:Secret"]!;   // same key
    options.JwtIssuer = "agone";
    options.JwtAudience = "agone-products";
    options.CacheDuration = TimeSpan.FromMinutes(5);
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseAGOnePermissionRefresh();   // <-- detects version changes
```

## Usage

### 1. Attribute-based (Controllers / Minimal APIs)

```csharp
using AGOne.Authorization.Constants;
using AGOne.Authorization.Handlers;

[ApiController]
[Route("api/employees")]
public class EmployeeController : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.Work.Employee.Read)]
    public async Task<IActionResult> GetAll() { ... }

    [HttpPost]
    [RequirePermission(Permissions.Work.Employee.Create)]
    public async Task<IActionResult> Create(CreateEmployeeDto dto) { ... }

    [HttpPut("{id}")]
    [RequirePermission(Permissions.Work.Employee.Update)]
    public async Task<IActionResult> Update(Guid id, UpdateEmployeeDto dto) { ... }

    [HttpDelete("{id}")]
    [RequirePermission(Permissions.Work.Employee.Delete)]
    public async Task<IActionResult> Delete(Guid id) { ... }
}
```

### 2. Injected service (Services / Blazor components)

```csharp
using AGOne.Authorization.Services;
using AGOne.Authorization.Constants;

public class EmployeeService
{
    private readonly IPermissionService _permissions;

    public EmployeeService(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    public async Task<Employee> GetEmployeeAsync(Guid id)
    {
        if (!await _permissions.HasPermissionAsync(Permissions.Work.Employee.Read))
            throw new UnauthorizedAccessException("No permission to read employees");

        // ... fetch employee
    }
}
```

### 3. Blazor component

```razor
@inject IPermissionService PermissionService

@if (_canCreate)
{
    <button @onclick="CreateEmployee">Add Employee</button>
}

@code {
    private bool _canCreate;

    protected override async Task OnInitializedAsync()
    {
        _canCreate = await PermissionService.HasPermissionAsync(Permissions.Work.Employee.Create);
    }
}
```

### 4. AG ONE Gateway — Token issuance at login

```csharp
using AGOne.Authorization.Services;

public class AuthController : ControllerBase
{
    private readonly IPermissionService _permissions;
    private readonly IJwtPermissionTokenService _tokenService;

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        // ... validate credentials, get user ...

        var permSet = await _permissions.GetUserPermissionsAsync(user.Id, user.TenantId);

        var token = _tokenService.GenerateToken(
            user.Id, user.TenantId, user.Email, user.Roles, permSet);

        Response.Cookies.Append("agone_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return Ok(new { token });
    }
}
```

### 5. AG ONE Gateway — Internal API for downstream refresh

```csharp
[ApiController]
[Route("api/internal/permissions")]
public class InternalPermissionsController : ControllerBase
{
    private readonly IPermissionService _permissions;

    [HttpGet("{tenantId}/{userId}")]
    public async Task<IActionResult> GetUserPermissions(Guid tenantId, Guid userId)
    {
        var perms = await _permissions.GetUserPermissionsAsync(userId, tenantId);
        return Ok(perms);
    }
}
```

### 6. Invalidate after role change

```csharp
public class RoleService
{
    private readonly IPermissionService _permissions;

    public async Task UpdateRolePermissions(Guid roleId, List<Guid> permissionIds)
    {
        // ... update role permissions in DB ...

        // Invalidate all affected users
        var affectedUsers = await _db.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => new { ur.UserId, ur.TenantId })
            .ToListAsync();

        foreach (var u in affectedUsers)
            await _permissions.InvalidateUserPermissionsAsync(u.UserId, u.TenantId);
    }
}
```

// ═══════════════════════════════════════════════════════════════════════════════
// AG ONE SSO MIDDLEWARE — Single-file, plug-and-play SSO for all AG ONE products
// ═══════════════════════════════════════════════════════════════════════════════
//
// HOW TO USE:
//   1. Copy this file into your Server project
//   2. Add NuGet: dotnet add package Microsoft.IdentityModel.Protocols.OpenIdConnect
//   3. Add config to appsettings.json (see bottom of this file)
//   4. In Program.cs:
//
//        // ── Services ──
//        builder.Services.AddAgOneSso(builder.Configuration);
//
//        // ── Pipeline ──
//        app.UseRouting();
//        app.UseCors();
//        app.UseAgOneSso();          // after UseRouting, before UseAuthorization
//        app.UseAuthorization();
//        app.MapControllers();
//        app.MapAgOneSsoEndpoints(); // after MapControllers
//        app.MapFallbackToFile("index.html");
//
//   5. In your Blazor WASM Client Program.cs:
//
//        builder.Services.AddTransient<CookieHandler>();
//        builder.Services.AddHttpClient("Backend",
//            c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
//            .AddHttpMessageHandler<CookieHandler>();
//        builder.Services.AddScoped(sp =>
//            sp.GetRequiredService<IHttpClientFactory>().CreateClient("Backend"));
//
//      Where CookieHandler is:
//        public class CookieHandler : DelegatingHandler
//        {
//            protected override Task<HttpResponseMessage> SendAsync(
//                HttpRequestMessage request, CancellationToken ct)
//            {
//                request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
//                return base.SendAsync(request, ct);
//            }
//        }
//
// ═══════════════════════════════════════════════════════════════════════════════

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AgOne.Sso;

// ─────────────────────────────────────────────────────────────────────────────
// OPTIONS
// ─────────────────────────────────────────────────────────────────────────────

public class AgOneSsoOptions
{
    public const string SectionName = "AgOneSso";

    public string AgOneBaseUrl { get; set; } = "";
    public string AgOneLoginUrl { get; set; } = "";
    public string TokenValidateEndpoint { get; set; } = "api/auth/external/validate";

    // Entra ID
    public string Authority { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ValidAudience { get; set; } = "";
    public List<string> AdditionalAudiences { get; set; } = new();

    // Cookies
    public string SessionCookieName { get; set; } = "agone_sso_token";
    public TimeSpan SessionCookieLifetime { get; set; } = TimeSpan.FromMinutes(60);
    public string? CookieDomain { get; set; }
    public SameSiteMode CookieSameSite { get; set; } = SameSiteMode.None;

    // Behavior
    public int RefreshBufferMinutes { get; set; } = 5;
    public bool IsAgOneGateway { get; set; } = false;
    public bool AcceptTokenFromQueryString { get; set; } = true;
    public string TokenQueryParameterName { get; set; } = "token";
    public List<string> AnonymousPaths { get; set; } = new();
    public List<string> PublicPaths { get; set; } = new();

    // Helpers
    internal string EffectiveLoginUrl => !string.IsNullOrEmpty(AgOneLoginUrl) ? AgOneLoginUrl : AgOneBaseUrl.TrimEnd('/');
    internal string EffectiveAuthority => !string.IsNullOrEmpty(Authority) ? Authority : !string.IsNullOrEmpty(TenantId) ? $"https://login.microsoftonline.com/{TenantId}/v2.0" : "";
    internal IEnumerable<string> AllAudiences
    {
        get
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(ValidAudience)) list.Add(ValidAudience);
            if (!string.IsNullOrEmpty(ClientId)) { list.Add(ClientId); list.Add($"api://{ClientId}"); }
            list.AddRange(AdditionalAudiences);
            return list.Distinct();
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public class SsoTokenRequest
{
    [JsonPropertyName("token")] public string? Token { get; set; }
    [JsonPropertyName("idToken")] public string? IdToken { get; set; }
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    [JsonPropertyName("tenantId")] public string? TenantId { get; set; }
}

public class SsoTokenResponse
{
    [JsonPropertyName("token")] public string? Token { get; set; }
    [JsonPropertyName("isValid")] public bool IsValid { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class SsoUserInfo
{
    [JsonPropertyName("isAuthenticated")] public bool IsAuthenticated { get; set; }
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("roles")] public List<string> Roles { get; set; } = new();
    [JsonPropertyName("claims")] public Dictionary<string, string> Claims { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// TOKEN FORWARDING HANDLER — auto-attaches Bearer token to all outgoing HTTP
// calls from your server (Server → AG ONE API, Server → any external API)
//
// Reads token from:
//   1. HttpContext.Items["agone_token"] (set by middleware below)
//   2. Cookie fallback
//   3. Incoming Authorization header fallback
// ─────────────────────────────────────────────────────────────────────────────

public class AgOneTokenForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _http;

    public AgOneTokenForwardingHandler(IHttpContextAccessor http)
    {
        _http = http;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.Authorization != null)
            return base.SendAsync(request, ct);

        var ctx = _http.HttpContext;
        if (ctx == null)
            return base.SendAsync(request, ct);

        string? token = null;

        // 1. Token stored by middleware in HttpContext.Items
        if (ctx.Items.TryGetValue("agone_token", out var itemToken) && itemToken is string t1 && !string.IsNullOrEmpty(t1))
            token = t1;

        // 2. Cookie fallback
        if (string.IsNullOrEmpty(token))
        {
            var cookieName = ctx.RequestServices.GetService<IOptions<AgOneSsoOptions>>()?.Value.SessionCookieName ?? "agone_sso_token";
            ctx.Request.Cookies.TryGetValue(cookieName, out token);
        }

        // 3. Incoming Authorization header fallback
        if (string.IsNullOrEmpty(token))
        {
            var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
            if (auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                token = auth["Bearer ".Length..];
        }

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Trim().Trim('"'));

        return base.SendAsync(request, ct);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// THE MIDDLEWARE
// ─────────────────────────────────────────────────────────────────────────────

public class AgOneSsoMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AgOneSsoOptions _opts;
    private readonly ILogger<AgOneSsoMiddleware> _log;
    private readonly JwtSecurityTokenHandler _jwt = new();
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _oidc;

    private static readonly HashSet<string> StaticExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".css", ".wasm", ".dll", ".pdb", ".dat", ".json", ".ico",
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".woff", ".woff2",
        ".ttf", ".eot", ".map", ".br", ".gz", ".blat"
    };

    private static readonly string[] FrameworkPrefixes =
    {
        "/_framework", "/_content", "/_blazor", "/_vs",
        "/css", "/js", "/images", "/fonts", "/favicon.ico"
    };

    public AgOneSsoMiddleware(RequestDelegate next, IOptions<AgOneSsoOptions> opts, ILogger<AgOneSsoMiddleware> log)
    {
        _next = next;
        _opts = opts.Value;
        _log = log;

        var authority = _opts.EffectiveAuthority;
        if (!string.IsNullOrEmpty(authority))
        {
            _oidc = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{authority.TrimEnd('/')}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());
        }
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "/";

        // 1. Skip static files and framework paths
        if (IsAnonymous(path))
        {
            await _next(ctx);
            return;
        }

        // 2. Public path check
        var isPublicPath = IsPublicPath(path);

        // 3. Extract token
        var (token, source) = ExtractToken(ctx);

        if (string.IsNullOrEmpty(token))
        {
            if (isPublicPath) { await _next(ctx); return; }
            await Reject(ctx, "No authentication token found");
            return;
        }

        // 4. Validate
        var (principal, status) = await ValidateAsync(token);
        var activeToken = token;

        if (status == Status.ExpiringSoon)
        {
            var refreshed = await RefreshAsync(ctx, token);
            if (!string.IsNullOrEmpty(refreshed))
            {
                activeToken = refreshed;
                var (p2, _) = await ValidateAsync(refreshed);
                if (p2 != null) principal = p2;
            }
        }
        else if (status == Status.Expired)
        {
            var refreshed = await RefreshAsync(ctx, token);
            if (!string.IsNullOrEmpty(refreshed))
            {
                activeToken = refreshed;
                var (p2, _) = await ValidateAsync(refreshed);
                if (p2 != null) principal = p2;
                else if (!isPublicPath) { await Reject(ctx, "Refreshed token is invalid"); return; }
                else { await _next(ctx); return; }
            }
            else if (!isPublicPath) { await Reject(ctx, "Token expired and refresh failed"); return; }
            else { await _next(ctx); return; }
        }
        else if (status == Status.Invalid)
        {
            if (!isPublicPath) { await Reject(ctx, "Invalid token"); return; }
            await _next(ctx);
            return;
        }

        // 5. Set user + store token for downstream HttpClient calls
        if (principal != null) ctx.User = principal;
        ctx.Items["agone_token"] = activeToken;

        // 6. Set/refresh cookie
        SetSessionCookie(ctx, activeToken);

        // 7. Clean URL if token came from query string
        if (source == Src.Query)
        {
            ctx.Response.Redirect(CleanQueryString(ctx));
            return;
        }

        await _next(ctx);
    }

    // ═══════════ Token extraction ═══════════

    private enum Src { None, Header, Session, Query }

    private (string? token, Src source) ExtractToken(HttpContext ctx)
    {
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            var t = Clean(auth["Bearer ".Length..]);
            if (!string.IsNullOrEmpty(t)) return (t, Src.Header);
        }

        if (ctx.Request.Cookies.TryGetValue(_opts.SessionCookieName, out var sc) && !string.IsNullOrEmpty(sc))
            return (Clean(sc)!, Src.Session);

        if (_opts.AcceptTokenFromQueryString &&
            ctx.Request.Query.TryGetValue(_opts.TokenQueryParameterName, out var qt) &&
            !string.IsNullOrEmpty(qt.FirstOrDefault()))
            return (Clean(qt.FirstOrDefault()!)!, Src.Query);

        if (ctx.Request.Method == "POST" && ctx.Request.HasFormContentType &&
            ctx.Request.Form.TryGetValue("token", out var ft) && !string.IsNullOrEmpty(ft.FirstOrDefault()))
            return (Clean(ft.FirstOrDefault()!)!, Src.Query);

        return (null, Src.None);
    }

    private static string? Clean(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : token.Trim().Trim('"').Trim();

    // ═══════════ JWT validation ═══════════

    private enum Status { Valid, ExpiringSoon, Expired, Invalid }

    private async Task<(ClaimsPrincipal? principal, Status status)> ValidateAsync(string token)
    {
        try
        {
            if (_oidc != null)
            {
                try
                {
                    var config = await _oidc.GetConfigurationAsync(CancellationToken.None);
                    var tvp = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeys = config.SigningKeys,
                        ValidateIssuer = true,
                        ValidIssuers = new[]
                        {
                            _opts.EffectiveAuthority,
                            $"https://login.microsoftonline.com/{_opts.TenantId}/v2.0",
                            $"https://sts.windows.net/{_opts.TenantId}/"
                        },
                        ValidateAudience = _opts.AllAudiences.Any(),
                        ValidAudiences = _opts.AllAudiences,
                        ValidateLifetime = false,
                        ClockSkew = TimeSpan.FromMinutes(2)
                    };

                    var principal = _jwt.ValidateToken(token, tvp, out var validated);
                    return Classify(principal, (JwtSecurityToken)validated);
                }
                catch (SecurityTokenSignatureKeyNotFoundException)
                {
                    return ParseUnsigned(token);
                }
                catch (SecurityTokenInvalidSignatureException)
                {
                    return ParseUnsigned(token);
                }
            }

            return ParseUnsigned(token);
        }
        catch (SecurityTokenExpiredException) { return (null, Status.Expired); }
        catch { return (null, Status.Invalid); }
    }

    private (ClaimsPrincipal?, Status) ParseUnsigned(string token)
    {
        try
        {
            if (!_jwt.CanReadToken(token)) return (null, Status.Invalid);
            var jwt = _jwt.ReadJwtToken(token);
            return Classify(new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, "AgOneSso")), jwt);
        }
        catch { return (null, Status.Invalid); }
    }

    private (ClaimsPrincipal, Status) Classify(ClaimsPrincipal p, JwtSecurityToken jwt)
    {
        var now = DateTime.UtcNow;
        if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < now) return (p, Status.Expired);
        if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < now.AddMinutes(_opts.RefreshBufferMinutes)) return (p, Status.ExpiringSoon);
        return (p, Status.Valid);
    }

    // ═══════════ Token refresh ═══════════

    private async Task<string?> RefreshAsync(HttpContext ctx, string currentToken)
    {
        if (_opts.IsAgOneGateway) return null;

        try
        {
            var factory = ctx.RequestServices.GetService<IHttpClientFactory>();
            if (factory == null) return null;

            var client = factory.CreateClient("AgOneSso");
            var endpoint = _opts.TokenValidateEndpoint.TrimStart('/');

            string? userId = null, tenantId = null;
            try
            {
                if (_jwt.CanReadToken(currentToken))
                {
                    var parsed = _jwt.ReadJwtToken(currentToken);
                    userId = parsed.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                          ?? parsed.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                    tenantId = parsed.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value
                            ?? parsed.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
                }
            }
            catch { }

            var resp = await client.PostAsJsonAsync(endpoint,
                new SsoTokenRequest { Token = currentToken, UserId = userId, TenantId = tenantId },
                ctx.RequestAborted);

            if (!resp.IsSuccessStatusCode) return null;

            var result = await resp.Content.ReadFromJsonAsync<SsoTokenResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ctx.RequestAborted);

            return result?.Token;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AG ONE token refresh failed");
            return null;
        }
    }

    // ═══════════ Cookie ═══════════

    private void SetSessionCookie(HttpContext ctx, string token)
    {
        var co = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = _opts.CookieSameSite,
            Path = "/",
            MaxAge = _opts.SessionCookieLifetime,
            IsEssential = true
        };
        if (!string.IsNullOrEmpty(_opts.CookieDomain)) co.Domain = _opts.CookieDomain;
        ctx.Response.Cookies.Append(_opts.SessionCookieName, token, co);
    }

    // ═══════════ Reject ═══════════

    private async Task Reject(HttpContext ctx, string reason)
    {
        ctx.Response.Cookies.Delete(_opts.SessionCookieName,
            new CookieOptions { Path = "/", Secure = true, SameSite = _opts.CookieSameSite });

        if (IsApi(ctx))
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new { isAuthenticated = false, message = reason, loginUrl = _opts.EffectiveLoginUrl });
        }
        else
        {
            var returnUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.Path}{ctx.Request.QueryString}";
            ctx.Response.Redirect($"{_opts.EffectiveLoginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }
    }

    // ═══════════ Helpers ═══════════

    private bool IsAnonymous(string path)
    {
        foreach (var p in FrameworkPrefixes)
            if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var p in _opts.AnonymousPaths)
            if (path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && StaticExtensions.Contains(ext);
    }

    private bool IsPublicPath(string path)
    {
        foreach (var p in _opts.PublicPaths)
        {
            if (p == "/" && path == "/") return true;
            if (p != "/" && path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool IsApi(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        return path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || ctx.Request.Headers.Accept.Any(a => a?.Contains("application/json") == true)
            || ctx.Request.Headers.ContainsKey("X-Requested-With");
    }

    private string CleanQueryString(HttpContext ctx)
    {
        var keep = ctx.Request.Query
            .Where(q => !q.Key.Equals(_opts.TokenQueryParameterName, StringComparison.OrdinalIgnoreCase))
            .Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value.ToString())}")
            .ToList();
        var path = ctx.Request.Path.Value ?? "/";
        return keep.Count > 0 ? $"{path}?{string.Join("&", keep)}" : path;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// EXTENSION METHODS
// ─────────────────────────────────────────────────────────────────────────────

public static class AgOneSsoExtensions
{
    public static IServiceCollection AddAgOneSso(this IServiceCollection services, IConfiguration config)
        => services.AddAgOneSso(config, _ => { });

    public static IServiceCollection AddAgOneSso(this IServiceCollection services, IConfiguration config, Action<AgOneSsoOptions> configure)
    {
        services.Configure<AgOneSsoOptions>(o =>
        {
            config.GetSection(AgOneSsoOptions.SectionName).Bind(o);
            configure(o);
        });

        var opts = new AgOneSsoOptions();
        config.GetSection(AgOneSsoOptions.SectionName).Bind(opts);
        configure(opts);

        // Required for AgOneTokenForwardingHandler to access HttpContext
        services.AddHttpContextAccessor();

        // Register the token forwarding handler
        services.AddTransient<AgOneTokenForwardingHandler>();

        // ── HttpClient: middleware's own refresh calls to AG ONE ──
        if (!opts.IsAgOneGateway && !string.IsNullOrEmpty(opts.AgOneBaseUrl))
        {
            services.AddHttpClient("AgOneSso", c =>
            {
                c.BaseAddress = new Uri(opts.AgOneBaseUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(30);
                c.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        // ── HttpClient: "AgOneApi" — for YOUR services calling AG ONE APIs ──
        // Token is auto-forwarded from the current request's cookie/context.
        // Usage: var client = _factory.CreateClient("AgOneApi");
        if (!string.IsNullOrEmpty(opts.AgOneBaseUrl))
        {
            services.AddHttpClient("AgOneApi", c =>
            {
                c.BaseAddress = new Uri(opts.AgOneBaseUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<AgOneTokenForwardingHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        // ── HttpClient: "AuthorizedClient" — for any HTTP call that needs the token ──
        // Covers: calls from Blazor server-rendered pages, services, etc.
        // Token is auto-forwarded.
        services.AddHttpClient("AuthorizedClient")
            .AddHttpMessageHandler<AgOneTokenForwardingHandler>();

        return services;
    }

    public static IApplicationBuilder UseAgOneSso(this IApplicationBuilder app)
        => app.UseMiddleware<AgOneSsoMiddleware>();

    public static IEndpointRouteBuilder MapAgOneSsoEndpoints(this IEndpointRouteBuilder endpoints, string basePath = "api/auth")
    {
        var p = basePath.TrimEnd('/');

        endpoints.MapGet($"{p}/sso-user-info", (HttpContext ctx) =>
        {
            var user = ctx.User;
            if (user.Identity?.IsAuthenticated != true)
                return Results.Ok(new SsoUserInfo { IsAuthenticated = false });

            return Results.Ok(new SsoUserInfo
            {
                IsAuthenticated = true,
                UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("oid")?.Value
                      ?? user.FindFirst("sub")?.Value,
                Email = user.FindFirst(ClaimTypes.Email)?.Value
                      ?? user.FindFirst("preferred_username")?.Value
                      ?? user.FindFirst("email")?.Value,
                Name = user.FindFirst("name")?.Value
                      ?? user.FindFirst(ClaimTypes.Name)?.Value,
                Roles = user.FindAll(ClaimTypes.Role).Concat(user.FindAll("roles"))
                             .Select(c => c.Value).Distinct().ToList()
            });
        });

        endpoints.MapPost($"{p}/sso-logout", (HttpContext ctx, IOptions<AgOneSsoOptions> opts) =>
        {
            var o = opts.Value;
            ctx.Response.Cookies.Delete(o.SessionCookieName, new CookieOptions { Path = "/", Secure = true, SameSite = o.CookieSameSite });
            return Results.Ok(new { loggedOut = true, redirectUrl = o.EffectiveLoginUrl });
        });

        return endpoints;
    }
}

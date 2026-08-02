using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Remediation;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Domain.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

/// <summary>
/// Shared plumbing: the tenant currently in scope (the app is multi-tenant from day one) and the
/// signed-in principal in the shape the audit trail records.
/// </summary>
[Authorize]
public abstract class AgControllerBase : Controller
{
    private const string TenantCookie = "ag-tenant";

    protected readonly IAgOneSafeDbContext Db;

    protected AgControllerBase(IAgOneSafeDbContext db) => Db = db;

    protected Actor CurrentActor => new(
        User.Identity?.Name ?? "unknown",
        User.IsInRole(AgRoles.Ciso) ? AgRoles.Ciso
        : User.IsInRole(AgRoles.SecurityAdmin) ? AgRoles.SecurityAdmin
        : AgRoles.Auditor);

    protected async Task<TenantConnection?> GetCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await Db.TenantConnections
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);

        ViewBag.Tenants = tenants;

        if (tenants.Count == 0)
        {
            return null;
        }

        var selected = Request.Cookies.TryGetValue(TenantCookie, out var raw) && int.TryParse(raw, out var id)
            ? tenants.FirstOrDefault(t => t.Id == id) ?? tenants[0]
            : tenants[0];

        ViewBag.CurrentTenant = selected;
        return selected;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SwitchTenant(int tenantId, string? returnUrl)
    {
        Response.Cookies.Append(TenantCookie, tenantId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}

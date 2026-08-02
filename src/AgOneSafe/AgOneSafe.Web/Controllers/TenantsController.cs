using System.Text;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Application.Tenants;
using AgOneSafe.Domain;
using AgOneSafe.Domain.Identity;
using AgOneSafe.Domain.Tenants;
using AgOneSafe.Infrastructure.Emulation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgOneSafe.Web.Controllers;

public class TenantsController : AgControllerBase
{
    private readonly ITenantConnectionService _tenants;
    private readonly ITenantEmulator _emulator;

    public TenantsController(IAgOneSafeDbContext db, ITenantConnectionService tenants, ITenantEmulator emulator)
        : base(db)
    {
        _tenants = tenants;
        _emulator = emulator;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);

        var tenants = await Db.TenantConnections
            .AsNoTracking()
            .Include(t => t.HealthChecks)
            .OrderBy(t => t.DisplayName)
            .ToListAsync(cancellationToken);

        return View(tenants);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);

        var tenant = await Db.TenantConnections
            .AsNoTracking()
            .Include(t => t.HealthChecks)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return tenant is null ? NotFound() : View(tenant);
    }

    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        await GetCurrentTenantAsync(cancellationToken);
        return View(new TenantOnboardingForm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> Connect(TenantOnboardingForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await GetCurrentTenantAsync(cancellationToken);
            return View(form);
        }

        var tenant = await _tenants.OnboardAsync(new TenantOnboardingRequest
        {
            DisplayName = form.DisplayName,
            TenantId = form.TenantId,
            PrimaryDomain = form.PrimaryDomain,
            ClientId = form.ClientId,
            SecretReference = form.SecretReference,
            AzureSubscriptionIds = form.AzureSubscriptionIds,
            LighthouseEnabled = form.LighthouseEnabled,
            LicenseSkus = form.LicenseSkus,
            TargetLevel = form.TargetLevel,
            ExecutionMode = form.ExecutionMode,
            RequestedBy = CurrentActor.Name
        }, cancellationToken);

        TempData["Toast"] = $"{tenant.DisplayName} connected and health checked.";
        return RedirectToAction(nameof(Details), new { id = tenant.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> RunHealthCheck(int id, CancellationToken cancellationToken)
    {
        var checks = await _tenants.RunHealthChecksAsync(id, cancellationToken);
        TempData["Toast"] = $"{checks.Count(c => c.IsHealthy)} of {checks.Count} connectors are healthy.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> ResetEmulator(int id, CancellationToken cancellationToken)
    {
        var tenant = await Db.TenantConnections.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        await _emulator.ResetAsync(tenant, cancellationToken);
        TempData["Toast"] = "Emulated tenant state reset to the seeded baseline.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = AgRoles.CanExecute)]
    public async Task<IActionResult> OnboardingScript(int id, CancellationToken cancellationToken)
    {
        var tenant = await Db.TenantConnections.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        var scopes = await Db.ControlActions
            .AsNoTracking()
            .Where(a => a.ControlDefinition!.IsEnabled && a.RequiredScopes != string.Empty)
            .Select(a => a.RequiredScopes)
            .ToListAsync(cancellationToken);

        var distinct = scopes
            .SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        var script = BuildOnboardingScript(tenant, distinct);

        return File(Encoding.UTF8.GetBytes(script), "text/plain", $"connect-agonesafe-{tenant.PrimaryDomain}.ps1");
    }

    /// <summary>
    /// Generates the least-privilege onboarding script for a customer tenant. The scope list is
    /// derived from the catalog itself, so a tenant is never asked to consent to a permission that
    /// no enabled control actually uses.
    /// </summary>
    private static string BuildOnboardingScript(TenantConnection tenant, IReadOnlyList<string> scopes)
    {
        var builder = new StringBuilder();

        builder.AppendLine("<#");
        builder.AppendLine("    AG ONE Safe - tenant onboarding");
        builder.AppendLine($"    Tenant : {tenant.DisplayName} ({tenant.PrimaryDomain})");
        builder.AppendLine($"    Issued : {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        builder.AppendLine("    Run this as a Global Administrator of the customer tenant. It creates the");
        builder.AppendLine("    AG ONE Safe service principal, grants only the scopes the enabled control");
        builder.AppendLine("    catalog needs, and prints the values to paste back into the platform.");
        builder.AppendLine("#>");
        builder.AppendLine();
        builder.AppendLine("param(");
        builder.AppendLine($"    [string] $TenantId = '{tenant.TenantId}',");
        builder.AppendLine($"    [string] $ApplicationName = 'AG ONE Safe'");
        builder.AppendLine(")");
        builder.AppendLine();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("Connect-MgGraph -TenantId $TenantId -Scopes 'Application.ReadWrite.All','AppRoleAssignment.ReadWrite.All','Directory.ReadWrite.All'");
        builder.AppendLine();
        builder.AppendLine("$app = New-MgApplication -DisplayName $ApplicationName -SignInAudience AzureADMultipleOrgs");
        builder.AppendLine("$sp  = New-MgServicePrincipal -AppId $app.AppId");
        builder.AppendLine();
        builder.AppendLine("# Scopes required by the enabled control catalog");
        builder.AppendLine("$requiredScopes = @(");

        foreach (var scope in scopes)
        {
            builder.AppendLine($"    '{scope.Replace("'", "''")}',");
        }

        builder.AppendLine("    'Directory.Read.All'");
        builder.AppendLine(")");
        builder.AppendLine();
        builder.AppendLine("Write-Host \"Grant admin consent for:\" -ForegroundColor Cyan");
        builder.AppendLine("$requiredScopes | ForEach-Object { Write-Host \"  - $_\" }");
        builder.AppendLine();
        builder.AppendLine("Write-Host \"Consent URL:\" -ForegroundColor Cyan");
        builder.AppendLine("Write-Host \"https://login.microsoftonline.com/$TenantId/adminconsent?client_id=$($app.AppId)\"");
        builder.AppendLine();

        if (tenant.LighthouseEnabled)
        {
            builder.AppendLine("# Azure Lighthouse is required for cross-tenant Azure resource remediation.");
            builder.AppendLine("# Deploy the delegation template before running Azure benchmark remediation:");
            builder.AppendLine("#   New-AzSubscriptionDeployment -Name 'agone-lighthouse' -Location 'westeurope' `");
            builder.AppendLine("#       -TemplateFile ./deploy/lighthouse-delegation.json");
            builder.AppendLine();
        }

        builder.AppendLine("Write-Host \"Paste these into AG ONE Safe > Tenants > Connect:\" -ForegroundColor Green");
        builder.AppendLine("[pscustomobject]@{ TenantId = $TenantId; ClientId = $app.AppId; ObjectId = $sp.Id } | Format-List");

        return builder.ToString();
    }
}

public class TenantOnboardingForm
{
    [System.ComponentModel.DataAnnotations.Required]
    public string DisplayName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string TenantId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string PrimaryDomain { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string ClientId { get; set; } = string.Empty;

    public string SecretReference { get; set; } = string.Empty;
    public string AzureSubscriptionIds { get; set; } = string.Empty;
    public bool LighthouseEnabled { get; set; } = true;
    public string LicenseSkus { get; set; } = string.Empty;
    public ProfileLevel TargetLevel { get; set; } = ProfileLevel.L1;
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Simulation;
}

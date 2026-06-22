// FILE: TenantManagementController.cs → API/Controllers/

using System.Security.Claims;
using AGOne.Shared.DTOs;
using AGOne.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AGOne.Api.Controllers;

[ApiController]
[Route("api/tenant-management")]
[Authorize]
public class TenantManagementController : ControllerBase
{
    private readonly ITenantManagementService _service;

    public TenantManagementController(ITenantManagementService service)
    {
        _service = service;
    }

    private Guid? GetTenantId()
    {
        var tid = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tid, out var id) ? id : null;
    }

    // ═══ Company Profile ═══

    [HttpGet("company-profile")]
    public async Task<IActionResult> GetCompanyProfile()
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        return Ok(await _service.GetCompanyProfileAsync(tid.Value));
    }

    [HttpPut("company-profile")]
    public async Task<IActionResult> SaveCompanyProfile([FromBody] TenantManagement_CompanyProfileDto dto)
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        await _service.SaveCompanyProfileAsync(tid.Value, dto);
        return Ok(new { success = true, message = "Company profile saved" });
    }

    // ═══ SSO ═══

    [HttpGet("sso")]
    public async Task<IActionResult> GetSsoSettings()
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        return Ok(await _service.GetSsoSettingsAsync(tid.Value));
    }

    [HttpPut("sso")]
    public async Task<IActionResult> SaveSsoSettings([FromBody] TenantManagement_SsoSettingsDto dto)
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        await _service.SaveSsoSettingsAsync(tid.Value, dto);
        return Ok(new { success = true, message = "SSO settings saved" });
    }

    // ═══ API Keys ═══

    [HttpGet("api-keys")]
    public async Task<IActionResult> GetApiKeys()
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        return Ok(await _service.GetApiKeysAsync(tid.Value));
    }

    [HttpPost("api-keys")]
    public async Task<IActionResult> CreateApiKey([FromBody] TenantManagement_CreateApiKeyRequest request)
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        var result = await _service.CreateApiKeyAsync(tid.Value, request);
        return Ok(result);
    }

    [HttpDelete("api-keys/{keyId:guid}")]
    public async Task<IActionResult> RevokeApiKey(Guid keyId)
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        await _service.RevokeApiKeyAsync(tid.Value, keyId);
        return Ok(new { success = true, message = "API key revoked" });
    }

    // ═══ Audit ═══

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        var result = await _service.GetAuditLogsAsync(tid.Value, new TenantManagement_AuditQueryDto { Search = search, Page = page, PageSize = pageSize });
        return Ok(result);
    }

    // ═══ Login History ═══

    [HttpGet("login-history")]
    public async Task<IActionResult> GetLoginHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var tid = GetTenantId();
        if (tid == null) return Unauthorized();
        return Ok(await _service.GetLoginHistoryAsync(tid.Value, page, pageSize));
    }
}

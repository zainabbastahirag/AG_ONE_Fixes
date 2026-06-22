// ═══════════════════════════════════════════════════════════════════════════
// FILE: UserAccessController.cs
// GOES IN: API project (AGOne.Api/Controllers/)
// ═══════════════════════════════════════════════════════════════════════════

using System.Security.Claims;
using AGOne.Shared.DTOs;
using AGOne.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AGOne.Api.Controllers;

[ApiController]
[Route("api/users/access")]
[Authorize]
public class UserAccessController : ControllerBase
{
    private readonly IUserAccessService _service;

    public UserAccessController(IUserAccessService service)
    {
        _service = service;
    }

    private Guid? GetTenantId()
    {
        var tid = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tid, out var id) ? id : null;
    }

    [HttpGet("entities")]
    public async Task<IActionResult> GetEntities()
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return Unauthorized();
        return Ok(await _service.GetEntitiesAsync(tenantId.Value));
    }

    [HttpGet("users")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? search)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return Unauthorized();
        return Ok(await _service.SearchUsersAsync(tenantId.Value, search));
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return Unauthorized();
        return Ok(await _service.GetProductsAsync(tenantId.Value));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles([FromQuery] Guid? productId)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return Unauthorized();
        return Ok(await _service.GetRolesAsync(tenantId.Value, productId));
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignAccess([FromBody] AssignAccessRequest request)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return Unauthorized();
        var result = await _service.AssignAccessAsync(tenantId.Value, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("user-roles/{userId:guid}")]
    public async Task<IActionResult> GetUserExistingRoles(Guid userId)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return Unauthorized();
        return Ok(await _service.GetUserExistingRolesAsync(tenantId.Value, userId));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// FILE: UserAccessController.cs
// GOES IN: API project (AGOne.Api)
// Namespace: AGOne.Api.Controllers
//
// API endpoints for the Assign User Access wizard.
// Depends on: IUserAccessService (from Shared), IAGOnePermissionService
// ═══════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AGOne.Shared.Authorization;
using AGOne.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AGOne.Api.Controllers;

[ApiController]
[Route("api/users/access")]
[Authorize]
public class UserAccessController : ControllerBase
{
    private readonly IUserAccessService _userAccessService;
    private readonly IAGOnePermissionService _permissionService;
    private readonly ILogger<UserAccessController> _logger;

    public UserAccessController(
        IUserAccessService userAccessService,
        IAGOnePermissionService permissionService,
        ILogger<UserAccessController> logger)
    {
        _userAccessService = userAccessService;
        _permissionService = permissionService;
        _logger = logger;
    }

    /// <summary>
    /// Returns tenant entities (branches) for the entity dropdown in Step 1.
    /// </summary>
    [HttpGet("entities")]
    public async Task<ActionResult<List<AssignAccessEntityDto>>> GetEntities()
    {
        try
        {
            var tenantId = _permissionService.GetTenantId();
            var entities = await _userAccessService.GetEntitiesAsync(tenantId);
            return Ok(entities);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching entities for assign-access");
            return StatusCode(500, new { message = "Failed to load entities." });
        }
    }

    /// <summary>
    /// Searches users for the user dropdown in Step 1.
    /// Supports optional search text and entity (tenant) filter.
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<List<AssignAccessUserDto>>> SearchUsers(
        [FromQuery] string? search = null,
        [FromQuery] Guid? entityId = null)
    {
        try
        {
            var tenantId = _permissionService.GetTenantId();
            var users = await _userAccessService.SearchUsersAsync(tenantId, search, entityId);
            return Ok(users);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users for assign-access. Search={Search}, EntityId={EntityId}",
                search, entityId);
            return StatusCode(500, new { message = "Failed to search users." });
        }
    }

    /// <summary>
    /// Returns products the tenant is subscribed to for the product checklist in Step 2.
    /// </summary>
    [HttpGet("products")]
    public async Task<ActionResult<List<AssignAccessProductDto>>> GetProducts()
    {
        try
        {
            var tenantId = _permissionService.GetTenantId();
            var products = await _userAccessService.GetProductsAsync(tenantId);
            return Ok(products);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products for assign-access");
            return StatusCode(500, new { message = "Failed to load products." });
        }
    }

    /// <summary>
    /// Returns roles for the role checklist in Step 2.
    /// If productId is provided, filters to roles for that product.
    /// </summary>
    [HttpGet("roles")]
    public async Task<ActionResult<List<AssignAccessRoleDto>>> GetRoles(
        [FromQuery] Guid? productId = null)
    {
        try
        {
            var tenantId = _permissionService.GetTenantId();
            var roles = await _userAccessService.GetRolesAsync(tenantId, productId);
            return Ok(roles);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching roles for assign-access. ProductId={ProductId}", productId);
            return StatusCode(500, new { message = "Failed to load roles." });
        }
    }

    /// <summary>
    /// Assigns product/role combinations to one or more users.
    /// Handles duplicate detection (existing UserRole rows are skipped).
    /// </summary>
    [HttpPost("assign")]
    public async Task<ActionResult<AssignAccessResponse>> AssignAccess(
        [FromBody] AssignAccessRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.UserIds == null || request.UserIds.Count == 0)
            return BadRequest(new AssignAccessResponse
            {
                Success = false,
                Message = "At least one user must be selected."
            });

        if (request.Assignments == null || request.Assignments.Count == 0)
            return BadRequest(new AssignAccessResponse
            {
                Success = false,
                Message = "At least one product-role assignment is required."
            });

        try
        {
            var tenantId = _permissionService.GetTenantId();
            var response = await _userAccessService.AssignAccessAsync(tenantId, request);

            if (!response.Success)
            {
                _logger.LogWarning("Assign access returned failure: {Message}", response.Message);
                return BadRequest(response);
            }

            _logger.LogInformation(
                "Access assigned: {AssignedCount} new, {SkippedCount} duplicates skipped for {UserCount} user(s)",
                response.AssignedCount, response.SkippedDuplicateCount, request.UserIds.Count);

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning access for {UserCount} users", request.UserIds.Count);
            return StatusCode(500, new AssignAccessResponse
            {
                Success = false,
                Message = "An unexpected error occurred while assigning access."
            });
        }
    }
}

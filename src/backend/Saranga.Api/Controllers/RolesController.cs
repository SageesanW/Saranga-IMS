using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Roles;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    [RequirePermission(Permissions.RoleView)]
    public async Task<ActionResult<List<RoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _roleService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.RoleView)]
    public async Task<ActionResult<RoleDto>> GetRoleById(Guid id, CancellationToken cancellationToken)
    {
        var role = await _roleService.GetRoleByIdAsync(id, cancellationToken);
        return Ok(role);
    }

    [HttpPost]
    [RequirePermission(Permissions.RoleCreate)]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleService.CreateRoleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetRoleById), new { id = role.Id }, role);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.RoleEdit)]
    public async Task<ActionResult<RoleDto>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleService.UpdateRoleAsync(id, request, cancellationToken);
        return Ok(role);
    }

    [HttpPost("{id:guid}/permissions")]
    [RequirePermission(Permissions.RoleAssignPermissions)]
    public async Task<ActionResult<RoleDto>> AssignPermissions(Guid id, [FromBody] AssignRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleService.AssignPermissionsAsync(id, request, cancellationToken);
        return Ok(role);
    }
}

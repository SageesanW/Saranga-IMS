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
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpGet]
    [RequirePermission(Permissions.PermissionView)]
    public async Task<ActionResult<List<PermissionDto>>> GetPermissions(CancellationToken cancellationToken)
    {
        var permissions = await _permissionService.GetPermissionsAsync(cancellationToken);
        return Ok(permissions);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Users;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [RequirePermission(Permissions.UserView)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers([FromQuery] UserFilterRequest filter, CancellationToken cancellationToken)
    {
        var result = await _userService.GetUsersAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.UserView)]
    public async Task<ActionResult<UserDto>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserByIdAsync(id, cancellationToken);
        return Ok(user);
    }

    [HttpPost]
    [RequirePermission(Permissions.UserCreate)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.UserEdit)]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateUserAsync(id, request, cancellationToken);
        return Ok(user);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.UserToggleStatus)]
    public async Task<ActionResult<UserDto>> ToggleUserStatus(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.ToggleUserStatusAsync(id, cancellationToken);
        return Ok(user);
    }

    [HttpPost("{id:guid}/roles")]
    [RequirePermission(Permissions.UserAssignRoles)]
    public async Task<ActionResult<UserDto>> AssignRoles(Guid id, [FromBody] AssignUserRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.AssignRolesAsync(id, request, cancellationToken);
        return Ok(user);
    }

    [HttpPost("{id:guid}/branches")]
    [RequirePermission(Permissions.UserAssignBranches)]
    public async Task<ActionResult<UserDto>> AssignBranches(Guid id, [FromBody] AssignUserBranchesRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.AssignBranchesAsync(id, request, cancellationToken);
        return Ok(user);
    }

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission(Permissions.UserResetPassword)]
    public async Task<ActionResult> ResetPassword(Guid id, [FromBody] ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.ResetPasswordAsync(id, request, cancellationToken);
        return Ok(new { message = "Password reset successfully." });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Branches;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService)
    {
        _branchService = branchService;
    }

    [HttpGet]
    [RequirePermission(Permissions.BranchView)]
    public async Task<ActionResult<List<BranchDto>>> GetBranches([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var branches = await _branchService.GetBranchesAsync(isActive, cancellationToken);
        return Ok(branches);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.BranchView)]
    public async Task<ActionResult<BranchDto>> GetBranchById(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branchService.GetBranchByIdAsync(id, cancellationToken);
        return Ok(branch);
    }

    [HttpPost]
    [RequirePermission(Permissions.BranchCreate)]
    public async Task<ActionResult<BranchDto>> CreateBranch([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branchService.CreateBranchAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetBranchById), new { id = branch.Id }, branch);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.BranchEdit)]
    public async Task<ActionResult<BranchDto>> UpdateBranch(Guid id, [FromBody] UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        var branch = await _branchService.UpdateBranchAsync(id, request, cancellationToken);
        return Ok(branch);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.BranchToggleStatus)]
    public async Task<ActionResult<BranchDto>> ToggleBranchStatus(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _branchService.ToggleBranchStatusAsync(id, cancellationToken);
        return Ok(branch);
    }
}

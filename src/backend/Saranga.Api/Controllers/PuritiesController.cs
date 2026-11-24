using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Attributes;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PuritiesController : ControllerBase
{
    private readonly IAttributesService _attributesService;

    public PuritiesController(IAttributesService attributesService)
    {
        _attributesService = attributesService;
    }

    [HttpGet]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<List<PurityDto>>> GetPurities([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var list = await _attributesService.GetPuritiesAsync(isActive, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<PurityDto>> GetPurityById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.GetPurityByIdAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost]
    [RequirePermission(Permissions.AttributesCreate)]
    public async Task<ActionResult<PurityDto>> CreatePurity([FromBody] CreatePurityRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.CreatePurityAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPurityById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AttributesEdit)]
    public async Task<ActionResult<PurityDto>> UpdatePurity(Guid id, [FromBody] UpdatePurityRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.UpdatePurityAsync(id, request, cancellationToken);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.AttributesActivateDeactivate)]
    public async Task<ActionResult<PurityDto>> TogglePurityStatus(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.TogglePurityStatusAsync(id, cancellationToken);
        return Ok(item);
    }
}

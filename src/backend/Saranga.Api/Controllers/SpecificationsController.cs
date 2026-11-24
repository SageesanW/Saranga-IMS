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
public class SpecificationsController : ControllerBase
{
    private readonly IAttributesService _attributesService;

    public SpecificationsController(IAttributesService attributesService)
    {
        _attributesService = attributesService;
    }

    [HttpGet]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<List<SpecificationDto>>> GetSpecifications([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var list = await _attributesService.GetSpecificationsAsync(isActive, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<SpecificationDto>> GetSpecificationById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.GetSpecificationByIdAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost]
    [RequirePermission(Permissions.AttributesCreate)]
    public async Task<ActionResult<SpecificationDto>> CreateSpecification([FromBody] CreateSpecificationRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.CreateSpecificationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSpecificationById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AttributesEdit)]
    public async Task<ActionResult<SpecificationDto>> UpdateSpecification(Guid id, [FromBody] UpdateSpecificationRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.UpdateSpecificationAsync(id, request, cancellationToken);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.AttributesActivateDeactivate)]
    public async Task<ActionResult<SpecificationDto>> ToggleSpecificationStatus(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.ToggleSpecificationStatusAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost("{id:guid}/options")]
    [RequirePermission(Permissions.AttributesCreate)]
    public async Task<ActionResult<SpecificationOptionDto>> AddOption(Guid id, [FromBody] CreateSpecificationOptionRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.AddSpecificationOptionAsync(id, request, cancellationToken);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/options/{optionId:guid}/status")]
    [RequirePermission(Permissions.AttributesActivateDeactivate)]
    public async Task<ActionResult<SpecificationOptionDto>> ToggleOptionStatus(Guid id, Guid optionId, CancellationToken cancellationToken)
    {
        var item = await _attributesService.ToggleSpecificationOptionStatusAsync(id, optionId, cancellationToken);
        return Ok(item);
    }
}

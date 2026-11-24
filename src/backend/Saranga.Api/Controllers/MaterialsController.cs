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
public class MaterialsController : ControllerBase
{
    private readonly IAttributesService _attributesService;

    public MaterialsController(IAttributesService attributesService)
    {
        _attributesService = attributesService;
    }

    [HttpGet]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<List<MaterialDto>>> GetMaterials([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var list = await _attributesService.GetMaterialsAsync(isActive, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<MaterialDto>> GetMaterialById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.GetMaterialByIdAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost]
    [RequirePermission(Permissions.AttributesCreate)]
    public async Task<ActionResult<MaterialDto>> CreateMaterial([FromBody] CreateMaterialRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.CreateMaterialAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMaterialById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AttributesEdit)]
    public async Task<ActionResult<MaterialDto>> UpdateMaterial(Guid id, [FromBody] UpdateMaterialRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.UpdateMaterialAsync(id, request, cancellationToken);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.AttributesActivateDeactivate)]
    public async Task<ActionResult<MaterialDto>> ToggleMaterialStatus(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.ToggleMaterialStatusAsync(id, cancellationToken);
        return Ok(item);
    }
}

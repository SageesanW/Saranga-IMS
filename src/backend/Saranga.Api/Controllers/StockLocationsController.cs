using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Attributes;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/stock-locations")]
[Authorize]
public class StockLocationsController : ControllerBase
{
    private readonly IAttributesService _attributesService;

    public StockLocationsController(IAttributesService attributesService)
    {
        _attributesService = attributesService;
    }

    [HttpGet]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<List<StockLocationDto>>> GetStockLocations(
        [FromQuery] Guid? branchId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var list = await _attributesService.GetStockLocationsAsync(branchId, isActive, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<StockLocationDto>> GetStockLocationById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.GetStockLocationByIdAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost]
    [RequirePermission(Permissions.AttributesCreate)]
    public async Task<ActionResult<StockLocationDto>> CreateStockLocation([FromBody] CreateStockLocationRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.CreateStockLocationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetStockLocationById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AttributesEdit)]
    public async Task<ActionResult<StockLocationDto>> UpdateStockLocation(Guid id, [FromBody] UpdateStockLocationRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.UpdateStockLocationAsync(id, request, cancellationToken);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.AttributesActivateDeactivate)]
    public async Task<ActionResult<StockLocationDto>> ToggleStockLocationStatus(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.ToggleStockLocationStatusAsync(id, cancellationToken);
        return Ok(item);
    }
}

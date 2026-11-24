using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Inventory;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<PagedResult<InventoryItemDto>>> GetInventory(
        [FromQuery] InventoryQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetInventoryAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<InventoryItemDto>> GetInventoryItemById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventoryService.GetInventoryItemByIdAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost]
    [RequirePermission(Permissions.InventoryCreate)]
    public async Task<ActionResult<InventoryItemDto>> CreateInventoryItem(
        [FromBody] CreateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventoryService.CreateInventoryItemAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetInventoryItemById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.InventoryEdit)]
    public async Task<ActionResult<InventoryItemDto>> UpdateInventoryItem(
        Guid id,
        [FromBody] UpdateInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventoryService.UpdateInventoryItemAsync(id, request, cancellationToken);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/toggle-active")]
    [RequirePermission(Permissions.InventoryEdit)]
    public async Task<ActionResult<InventoryItemDto>> ToggleInventoryItemActive(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventoryService.ToggleInventoryItemActiveAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.InventoryEdit)]
    public async Task<ActionResult<InventoryItemDto>> ToggleInventoryItemStatus(Guid id, CancellationToken cancellationToken)
    {
        var item = await _inventoryService.ToggleInventoryItemActiveAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost("{id:guid}/admin-status-correction")]
    [RequirePermission(Permissions.InventoryStatusOverride)]
    public async Task<ActionResult<InventoryItemDto>> AdminStatusCorrection(
        Guid id,
        [FromBody] AdminStatusCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _inventoryService.AdminStatusCorrectionAsync(id, request, cancellationToken);
        return Ok(item);
    }

    [HttpGet("lookup/barcode/{barcode}")]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<InventoryLookupDto>> LookupByBarcode(string barcode, CancellationToken cancellationToken)
    {
        var item = await _inventoryService.LookupByBarcodeAsync(barcode, cancellationToken);
        return Ok(item);
    }

    [HttpGet("lookup/tag/{tagNumber}")]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<InventoryLookupDto>> LookupByTag(string tagNumber, CancellationToken cancellationToken)
    {
        var item = await _inventoryService.LookupByTagAsync(tagNumber, cancellationToken);
        return Ok(item);
    }
}

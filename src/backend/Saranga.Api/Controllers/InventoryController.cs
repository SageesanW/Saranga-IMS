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
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var item = await _inventoryService.CreateInventoryItemAsync(request, idempotencyKey, cancellationToken);
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

    [HttpPost("upload-file")]
    [RequirePermission(Permissions.InventoryCreate, Permissions.InventoryEdit)]
    public async Task<ActionResult<FileUploadResultDto>> UploadInventoryMedia(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file was provided." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _inventoryService.UploadFileAsync(
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            isTemporary: true,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/media")]
    [RequirePermission(Permissions.InventoryEdit)]
    public async Task<ActionResult<InventoryItemMediaDto>> AddMediaToItem(
        Guid id,
        IFormFile file,
        [FromQuery] bool isPrimary,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file was provided." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _inventoryService.AddMediaToItemAsync(
            id,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            isPrimary,
            cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}/media/{mediaId:guid}")]
    [RequirePermission(Permissions.InventoryEdit)]
    public async Task<IActionResult> RemoveMediaFromItem(
        Guid id,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        await _inventoryService.RemoveMediaFromItemAsync(id, mediaId, cancellationToken);
        return NoContent();
    }

    [HttpPost("cleanup-temp-media")]
    [RequirePermission(Permissions.InventoryEdit, Permissions.InventoryCreate)]
    public async Task<ActionResult<object>> CleanupTempMedia(
        [FromQuery] int maxAgeHours = 2,
        CancellationToken cancellationToken = default)
    {
        var deletedCount = await _inventoryService.CleanupExpiredTempUploadsAsync(TimeSpan.FromHours(maxAgeHours), cancellationToken);
        return Ok(new { deletedCount, message = $"Successfully cleaned up {deletedCount} expired temporary files." });
    }
}

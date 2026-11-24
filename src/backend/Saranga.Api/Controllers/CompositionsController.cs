using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Compositions;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompositionsController : ControllerBase
{
    private readonly ICompositionService _compositionService;

    public CompositionsController(ICompositionService compositionService)
    {
        _compositionService = compositionService;
    }

    [HttpGet]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<PagedResult<InventoryCompositionDto>>> GetCompositions(
        [FromQuery] CompositionQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        var result = await _compositionService.GetCompositionsAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<InventoryCompositionDto>> GetCompositionById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _compositionService.GetCompositionByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("lookup/tag/{tagNumber}")]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<InventoryCompositionDto>> LookupByTag(string tagNumber, CancellationToken cancellationToken)
    {
        var result = await _compositionService.LookupByTagAsync(tagNumber, cancellationToken);
        return Ok(result);
    }

    [HttpGet("lookup/barcode/{barcode}")]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<InventoryCompositionDto>> LookupByBarcode(string barcode, CancellationToken cancellationToken)
    {
        var result = await _compositionService.LookupByBarcodeAsync(barcode, cancellationToken);
        return Ok(result);
    }

    [HttpGet("eligible-children")]
    [RequirePermission(Permissions.InventoryView)]
    public async Task<ActionResult<List<EligibleChildItemDto>>> GetEligibleChildren(
        [FromQuery] EligibleChildQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        var result = await _compositionService.GetEligibleChildrenAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.InventoryCompositionCreate, Permissions.InventoryCreate)]
    public async Task<ActionResult<InventoryCompositionDto>> CreateComposition(
        [FromBody] CreateCompositionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _compositionService.CreateCompositionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCompositionById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.InventoryCompositionEdit, Permissions.InventoryEdit)]
    public async Task<ActionResult<InventoryCompositionDto>> UpdateComposition(
        Guid id,
        [FromBody] UpdateCompositionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _compositionService.UpdateCompositionAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/members")]
    [RequirePermission(Permissions.InventoryCompositionEdit, Permissions.InventoryEdit)]
    public async Task<ActionResult<InventoryCompositionDto>> AddMember(
        Guid id,
        [FromBody] AddCompositionMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _compositionService.AddMemberAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/members/{inventoryItemId:guid}")]
    [RequirePermission(Permissions.InventoryCompositionEdit, Permissions.InventoryEdit)]
    public async Task<ActionResult<InventoryCompositionDto>> RemoveMember(
        Guid id,
        Guid inventoryItemId,
        CancellationToken cancellationToken)
    {
        var result = await _compositionService.RemoveMemberAsync(id, inventoryItemId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/dissolve")]
    [RequirePermission(Permissions.InventoryCompositionDissolve, Permissions.InventoryEdit)]
    public async Task<ActionResult<InventoryCompositionDto>> DissolveComposition(
        Guid id,
        [FromBody] DissolveCompositionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _compositionService.DissolveCompositionAsync(id, request?.RowVersion, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Atomic POS domain operation: detached member when individually sold.
    /// If remaining active members falls below 2, automatically dissolves composition.
    /// </summary>
    [HttpPost("internal/detach-member-on-sale")]
    [RequirePermission(Permissions.InventoryEdit, Permissions.InventoryCompositionEdit)]
    public async Task<ActionResult<MemberDetachmentResultDto>> DetachMemberOnSale(
        [FromBody] DetachMemberOnSaleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _compositionService.DetachMemberOnSaleAsync(
            request.InventoryItemId,
            request.SaleInvoiceNumber,
            null,
            cancellationToken);

        if (result == null)
        {
            return NotFound(new { message = "Item is not an active member of any composition." });
        }

        return Ok(result);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.InventoryCount;
using Saranga.Domain.Constants;
using Saranga.Domain.Enums;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/inventory-counts")]
[Authorize]
public class InventoryCountController : ControllerBase
{
    private readonly IInventoryCountService _countService;

    public InventoryCountController(IInventoryCountService countService)
    {
        _countService = countService;
    }

    /// <summary>
    /// Lists inventory count sessions with pagination and branch/location/status filtering.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.InventoryCount, Permissions.InventoryView)]
    public async Task<ActionResult<PagedResult<InventoryCountDto>>> GetCounts(
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? stockLocationId,
        [FromQuery] InventoryCountStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new InventoryCountQueryParams(branchId, stockLocationId, status, page, pageSize);
        var result = await _countService.GetCountsAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Checks whether an active count is already running for a specified branch and stock location.
    /// </summary>
    [HttpGet("check-active")]
    [RequirePermission(Permissions.InventoryCount, Permissions.InventoryView)]
    public async Task<ActionResult<InventoryCountActiveCheckDto>> CheckActive(
        [FromQuery] Guid branchId,
        [FromQuery] Guid stockLocationId,
        CancellationToken cancellationToken)
    {
        var result = await _countService.CheckActiveCountForLocationAsync(branchId, stockLocationId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets detailed metrics and progress for a single inventory count session.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.InventoryCount, Permissions.InventoryView)]
    public async Task<ActionResult<InventoryCountDto>> GetCountById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _countService.GetCountByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves paginated frozen expected baseline lines for a count session.
    /// </summary>
    [HttpGet("{id:guid}/lines")]
    [RequirePermission(Permissions.InventoryCount, Permissions.InventoryView)]
    public async Task<ActionResult<PagedResult<InventoryCountLineDto>>> GetCountLines(
        Guid id,
        [FromQuery] bool? isCounted,
        [FromQuery] CountLineResult? result,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var lines = await _countService.GetCountLinesAsync(id, isCounted, result, page, pageSize, cancellationToken);
        return Ok(lines);
    }

    /// <summary>
    /// Retrieves unexpected items observed during a count session.
    /// </summary>
    [HttpGet("{id:guid}/unexpected")]
    [RequirePermission(Permissions.InventoryCount, Permissions.InventoryView)]
    public async Task<ActionResult<List<InventoryCountUnexpectedItemDto>>> GetUnexpectedItems(Guid id, CancellationToken cancellationToken)
    {
        var items = await _countService.GetUnexpectedItemsAsync(id, cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Retrieves all missing and unexpected variances for a count session requiring review.
    /// </summary>
    [HttpGet("{id:guid}/variances")]
    [RequirePermission(Permissions.InventoryCount, Permissions.InventoryView)]
    public async Task<ActionResult<CountVariancesDto>> GetVariances(Guid id, CancellationToken cancellationToken)
    {
        var variances = await _countService.GetCountVariancesAsync(id, cancellationToken);
        return Ok(variances);
    }

    /// <summary>
    /// Starts a new inventory count session, capturing the frozen baseline of expected physical items.
    /// Exactly one active count per branch and stock location is permitted.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.InventoryCount)]
    public async Task<ActionResult<InventoryCountDto>> StartCount(
        [FromBody] StartInventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _countService.StartCountAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCountById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Scans a barcode or tag number. Matches expected baseline item, records unexpected item,
    /// or resolves parent Assembly/Group members for presence verification.
    /// </summary>
    [HttpPost("{id:guid}/scan")]
    [RequirePermission(Permissions.InventoryCount)]
    public async Task<ActionResult<ScanResultDto>> ScanItem(
        Guid id,
        [FromBody] ScanInventoryItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _countService.ScanItemAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Confirms physically present child items from an Assembly or Group composition scan.
    /// Invariant: The composition parent itself is NEVER counted as an additional stock unit.
    /// </summary>
    [HttpPost("{id:guid}/scan-composition")]
    [RequirePermission(Permissions.InventoryCount)]
    public async Task<ActionResult<ConfirmCompositionScanResultDto>> ConfirmCompositionScan(
        Guid id,
        [FromBody] ConfirmCompositionScanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _countService.ConfirmCompositionScanAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reconciles/finishes count session. If 0 variances, automatically completes.
    /// If missing or unexpected variances exist, moves status to REQUIRES_REVIEW.
    /// </summary>
    [HttpPost("{id:guid}/finish")]
    [RequirePermission(Permissions.InventoryCount)]
    public async Task<ActionResult<InventoryCountDto>> FinishCount(Guid id, CancellationToken cancellationToken)
    {
        var result = await _countService.FinishCountAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Resolves a single Missing or Unexpected variance with a controlled reason code and audit notes.
    /// Strictly requires INVENTORY_VARIANCE_RESOLVE permission.
    /// Invariant: Variance resolution NEVER silently modifies the underlying InventoryItem.
    /// </summary>
    [HttpPost("{id:guid}/variances/resolve")]
    [RequirePermission(Permissions.InventoryVarianceResolve)]
    public async Task<ActionResult<InventoryCountDto>> ResolveVariance(
        Guid id,
        [FromBody] ResolveVarianceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _countService.ResolveVarianceAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Completes an inventory count in REQUIRES_REVIEW after 100% of missing and unexpected variances are resolved.
    /// Strictly requires INVENTORY_VARIANCE_RESOLVE permission.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [RequirePermission(Permissions.InventoryVarianceResolve)]
    public async Task<ActionResult<InventoryCountDto>> CompleteCount(
        Guid id,
        [FromBody] CompleteInventoryCountRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _countService.CompleteCountAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cancels an open or in-review count session with a mandatory audit reason.
    /// Historical count records and snapshot lines are permanently preserved.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(Permissions.InventoryCount)]
    public async Task<ActionResult<InventoryCountDto>> CancelCount(
        Guid id,
        [FromBody] CancelInventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _countService.CancelCountAsync(id, request, cancellationToken);
        return Ok(result);
    }
}

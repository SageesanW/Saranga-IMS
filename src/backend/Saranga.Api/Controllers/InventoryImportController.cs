using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Inventory;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/inventory/import")]
[Authorize]
public class InventoryImportController : ControllerBase
{
    private readonly IBulkImportService _bulkImportService;

    public InventoryImportController(IBulkImportService bulkImportService)
    {
        _bulkImportService = bulkImportService;
    }

    /// <summary>
    /// Downloads an Excel or CSV template for bulk inventory creation.
    /// </summary>
    [HttpGet("template")]
    [RequirePermission(Permissions.InventoryBulkImport, Permissions.InventoryView)]
    public async Task<IActionResult> DownloadTemplate([FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (content, contentType, fileName) = await _bulkImportService.GenerateTemplateAsync(format ?? "xlsx", cancellationToken);
        return File(content, contentType, fileName);
    }

    /// <summary>
    /// Uploads and parses an Excel (.xlsx) or CSV (.csv) file, runs full domain validation,
    /// persists a validated/failed batch record, and returns a detailed preview.
    /// Does NOT create inventory items yet.
    /// </summary>
    [HttpPost("validate")]
    [RequirePermission(Permissions.InventoryBulkImport)]
    [RequestSizeLimit(BulkImportConstants.MaxFileSizeBytes)] // 5MB
    public async Task<ActionResult<BulkImportValidationResultDto>> ValidateImport(
        [FromQuery] Guid branchId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please select a valid non-empty file to upload." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _bulkImportService.ValidateImportFileAsync(branchId, stream, file.FileName, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves validation results and preview rows for an existing batch.
    /// </summary>
    [HttpGet("batches/{id:guid}")]
    [RequirePermission(Permissions.InventoryBulkImport, Permissions.InventoryView)]
    public async Task<ActionResult<BulkImportValidationResultDto>> GetBatchValidation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _bulkImportService.GetBatchValidationAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Confirms and commits a fully valid batch transactionally, generating unique atomic
    /// TagNumbers and Barcodes for all items. Idempotent with Idempotency-Key.
    /// </summary>
    [HttpPost("confirm")]
    [RequirePermission(Permissions.InventoryBulkImport)]
    public async Task<ActionResult<BulkImportResultDto>> ConfirmImport(
        [FromBody] ConfirmBulkImportRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request == null || request.BatchId == Guid.Empty)
        {
            return BadRequest(new { message = "Valid BatchId is required to confirm import." });
        }

        // Also check alternative header name if Idempotency-Key not provided
        if (string.IsNullOrWhiteSpace(idempotencyKey) && Request.Headers.TryGetValue("X-Idempotency-Key", out var altKey))
        {
            idempotencyKey = altKey.ToString();
        }

        var result = await _bulkImportService.ConfirmImportAsync(request.BatchId, idempotencyKey, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lists recent bulk import batches for a branch or all accessible branches.
    /// </summary>
    [HttpGet("batches")]
    [RequirePermission(Permissions.InventoryBulkImport, Permissions.InventoryView)]
    public async Task<ActionResult<List<InventoryImportBatchDto>>> GetBatches(
        [FromQuery] Guid? branchId,
        CancellationToken cancellationToken)
    {
        var result = await _bulkImportService.GetBatchesAsync(branchId, cancellationToken);
        return Ok(result);
    }
}

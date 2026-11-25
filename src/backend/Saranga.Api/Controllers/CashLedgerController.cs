using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.CashLedger;
using Saranga.Domain.Constants;
using Saranga.Domain.Enums;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/cash-ledger")]
[Authorize]
public class CashLedgerController : ControllerBase
{
    private readonly ICashLedgerService _cashLedgerService;

    public CashLedgerController(ICashLedgerService cashLedgerService)
    {
        _cashLedgerService = cashLedgerService;
    }

    [HttpGet("transactions")]
    [Authorize(Policy = Permissions.CashLedgerView)]
    public async Task<IActionResult> GetTransactions([FromQuery] CashLedgerTransactionFilter filter, CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.GetTransactionsAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("transactions/{id:guid}")]
    [Authorize(Policy = Permissions.CashLedgerView)]
    public async Task<IActionResult> GetTransactionById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.GetTransactionByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("cash-in")]
    [Authorize(Policy = Permissions.CashLedgerCashIn)]
    public async Task<IActionResult> PostCashIn(
        [FromBody] PostCashInRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.PostCashInAsync(request, idempotencyKey, cancellationToken);
        return CreatedAtAction(nameof(GetTransactionById), new { id = result.Id }, result);
    }

    [HttpPost("cash-out")]
    [Authorize(Policy = Permissions.CashLedgerCashOut)]
    public async Task<IActionResult> PostCashOut(
        [FromBody] PostCashOutRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.PostCashOutAsync(request, idempotencyKey, cancellationToken);
        return CreatedAtAction(nameof(GetTransactionById), new { id = result.Id }, result);
    }

    [HttpPost("transfer")]
    [Authorize(Policy = Permissions.CashLedgerTransfer)]
    public async Task<IActionResult> PostTransfer(
        [FromBody] PostTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.PostTransferAsync(request, idempotencyKey, cancellationToken);
        return CreatedAtAction(nameof(GetTransactionById), new { id = result.Id }, result);
    }

    [HttpPost("transactions/{id:guid}/reverse")]
    [Authorize(Policy = Permissions.CashLedgerReverse)]
    public async Task<IActionResult> ReverseTransaction(
        Guid id,
        [FromBody] ReverseTransactionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.ReverseTransactionAsync(id, request, idempotencyKey, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shifts/{shiftId:guid}/summary")]
    [Authorize(Policy = Permissions.CashLedgerView)]
    public async Task<IActionResult> GetShiftSummary(Guid shiftId, CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.GetShiftSummaryAsync(shiftId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("categories")]
    [Authorize(Policy = Permissions.CashLedgerView)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] CashCategoryDirection? direction,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _cashLedgerService.GetCategoriesAsync(direction, includeInactive, cancellationToken);
        return Ok(result);
    }

    [HttpPost("categories")]
    [Authorize(Policy = Permissions.CashLedgerCategoryManage)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCashMovementCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.CreateCategoryAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("categories/{id:guid}")]
    [Authorize(Policy = Permissions.CashLedgerCategoryManage)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCashMovementCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.UpdateCategoryAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("categories/{id:guid}/status")]
    [Authorize(Policy = Permissions.CashLedgerCategoryManage)]
    public async Task<IActionResult> UpdateCategoryStatus(Guid id, [FromBody] UpdateCategoryStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _cashLedgerService.UpdateCategoryStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(result);
    }
}

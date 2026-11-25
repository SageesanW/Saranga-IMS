using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Pos;
using Saranga.Domain.Constants;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/pos")]
[Authorize]
public class PosController : ControllerBase
{
    private readonly IPosService _posService;

    public PosController(IPosService posService)
    {
        _posService = posService;
    }

    [HttpGet("lookup")]
    [Authorize(Policy = Permissions.PosView)]
    public async Task<IActionResult> LookupItem([FromQuery] Guid shiftId, [FromQuery] string query, CancellationToken cancellationToken)
    {
        var result = await _posService.LookupItemForSaleAsync(shiftId, query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("drafts")]
    [Authorize(Policy = Permissions.PosCreateSale)]
    public async Task<IActionResult> CreateDraftInvoice([FromBody] CreatePosDraftInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _posService.CreateDraftInvoiceAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetInvoiceById), new { id = result.Id }, result);
    }

    [HttpPost("drafts/quick-customer")]
    [Authorize(Policy = Permissions.PosCreateSale)]
    public async Task<IActionResult> QuickCreateCustomerAndDraft([FromBody] QuickCreateCustomerAndDraftRequest request, CancellationToken cancellationToken)
    {
        var result = await _posService.QuickCreateCustomerAndDraftAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetInvoiceById), new { id = result.Id }, result);
    }

    [HttpGet("invoices/{id:guid}")]
    [Authorize(Policy = Permissions.PosView)]
    public async Task<IActionResult> GetInvoiceById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _posService.GetInvoiceByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("invoices")]
    [Authorize(Policy = Permissions.PosView)]
    public async Task<IActionResult> GetInvoices([FromQuery] PosInvoiceFilter filter, CancellationToken cancellationToken)
    {
        var result = await _posService.GetInvoicesAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invoices/{id:guid}/lines")]
    [Authorize(Policy = Permissions.PosCreateSale)]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] AddPosInvoiceLineRequest request, CancellationToken cancellationToken)
    {
        var result = await _posService.AddLineToDraftAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("invoices/{id:guid}/lines/{lineId:guid}")]
    [Authorize(Policy = Permissions.PosCreateSale)]
    public async Task<IActionResult> UpdateLine(Guid id, Guid lineId, [FromBody] UpdatePosInvoiceLineRequest request, CancellationToken cancellationToken)
    {
        var result = await _posService.UpdateLineInDraftAsync(id, lineId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("invoices/{id:guid}/lines/{lineId:guid}")]
    [Authorize(Policy = Permissions.PosCreateSale)]
    public async Task<IActionResult> RemoveLine(Guid id, Guid lineId, CancellationToken cancellationToken)
    {
        var result = await _posService.RemoveLineFromDraftAsync(id, lineId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invoices/{id:guid}/finalize")]
    [Authorize(Policy = Permissions.PosCreateSale)]
    public async Task<IActionResult> FinalizeInvoice(
        Guid id,
        [FromBody] FinalizePosInvoiceRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessRuleException("An 'Idempotency-Key' request header is required to finalize an invoice.");
        }

        var result = await _posService.FinalizeInvoiceAsync(id, request, idempotencyKey, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invoices/{id:guid}/cancel")]
    [Authorize(Policy = Permissions.PosCreateSale)]
    public async Task<IActionResult> CancelDraftInvoice(Guid id, [FromBody] CancelInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _posService.CancelDraftInvoiceAsync(id, request.Reason, cancellationToken);
        return Ok(result);
    }
}

public record CancelInvoiceRequest(string Reason);

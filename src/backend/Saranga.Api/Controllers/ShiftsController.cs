using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Shifts;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [HttpPost("open")]
    [RequirePermission(Permissions.ShiftStart)]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShiftDto>> OpenShift(
        [FromBody] OpenShiftRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.OpenShiftAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:guid}/close")]
    [RequirePermission(Permissions.ShiftEnd, Permissions.ShiftOverrideClose)]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftDto>> CloseShift(
        Guid id,
        [FromBody] CloseShiftRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.CloseShiftAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.ShiftStart, Permissions.ShiftEnd, Permissions.CashLedgerView)]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftDto>> GetShiftById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.GetShiftByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("active-cashier")]
    [RequirePermission(Permissions.ShiftStart, Permissions.ShiftEnd)]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ShiftDto>> GetActiveShiftForCashier(
        [FromQuery] Guid? cashierId,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.GetActiveShiftForCashierAsync(cashierId, cancellationToken);
        if (result == null)
            return NoContent();

        return Ok(result);
    }

    [HttpGet("active-drawer/{drawerId:guid}")]
    [RequirePermission(Permissions.ShiftStart, Permissions.ShiftEnd, Permissions.CashLedgerView)]
    [ProducesResponseType(typeof(ShiftDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ShiftDto>> GetActiveShiftForDrawer(
        Guid drawerId,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.GetActiveShiftForDrawerAsync(drawerId, cancellationToken);
        if (result == null)
            return NoContent();

        return Ok(result);
    }

    [HttpGet("opening-defaults/{drawerId:guid}")]
    [RequirePermission(Permissions.ShiftStart)]
    [ProducesResponseType(typeof(ShiftOpeningDefaultsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftOpeningDefaultsDto>> GetOpeningDefaults(
        Guid drawerId,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.GetOpeningDefaultsAsync(drawerId, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [RequirePermission(Permissions.CashLedgerView, Permissions.ShiftStart, Permissions.ShiftEnd)]
    [ProducesResponseType(typeof(PagedResult<ShiftDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ShiftDto>>> GetShifts(
        [FromQuery] ShiftQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.GetShiftsAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    [HttpGet("drawers")]
    [RequirePermission(Permissions.ShiftStart, Permissions.ShiftEnd, Permissions.CashLedgerView, Permissions.CashDrawerManage)]
    [ProducesResponseType(typeof(List<CashDrawerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CashDrawerDto>>> GetCashDrawers(
        [FromQuery] Guid branchId,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.GetCashDrawersAsync(branchId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("drawers/{id:guid}")]
    [RequirePermission(Permissions.CashDrawerManage, Permissions.CashLedgerView)]
    [ProducesResponseType(typeof(CashDrawerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashDrawerDto>> GetCashDrawerById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.GetCashDrawerByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("drawers")]
    [RequirePermission(Permissions.CashDrawerManage)]
    [ProducesResponseType(typeof(CashDrawerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashDrawerDto>> CreateCashDrawer(
        [FromBody] CreateCashDrawerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.CreateCashDrawerAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("drawers/{id:guid}")]
    [RequirePermission(Permissions.CashDrawerManage)]
    [ProducesResponseType(typeof(CashDrawerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashDrawerDto>> UpdateCashDrawer(
        Guid id,
        [FromBody] UpdateCashDrawerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.UpdateCashDrawerAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("drawers/{id:guid}/status")]
    [RequirePermission(Permissions.CashDrawerManage)]
    [ProducesResponseType(typeof(CashDrawerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashDrawerDto>> UpdateCashDrawerStatus(
        Guid id,
        [FromBody] UpdateCashDrawerStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _shiftService.UpdateCashDrawerStatusAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("drawers/{id:guid}")]
    [RequirePermission(Permissions.CashDrawerManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCashDrawer(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _shiftService.DeleteCashDrawerAsync(id, cancellationToken);
        return NoContent();
    }
}

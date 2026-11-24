using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Rates;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/vat-configurations")]
[Authorize]
public class VatConfigurationsController : ControllerBase
{
    private readonly IVatConfigurationService _vatConfigurationService;

    public VatConfigurationsController(IVatConfigurationService vatConfigurationService)
    {
        _vatConfigurationService = vatConfigurationService;
    }

    [HttpGet]
    [RequirePermission(Permissions.VatConfig)]
    [ProducesResponseType(typeof(VatConfigurationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VatConfigurationDto>> GetActiveConfiguration(
        [FromQuery] Guid branchId,
        CancellationToken cancellationToken)
    {
        var result = await _vatConfigurationService.GetActiveVatConfigurationAsync(branchId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("history")]
    [RequirePermission(Permissions.VatConfig)]
    [ProducesResponseType(typeof(PagedResult<VatConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VatConfigurationDto>>> GetVatHistory(
        [FromQuery] VatHistoryQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        var result = await _vatConfigurationService.GetVatHistoryAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.VatConfig)]
    [ProducesResponseType(typeof(VatConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VatConfigurationDto>> CreateOrUpdateConfiguration(
        [FromBody] CreateVatConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _vatConfigurationService.CreateOrUpdateVatConfigurationAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("toggle")]
    [RequirePermission(Permissions.VatConfig)]
    [ProducesResponseType(typeof(VatConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VatConfigurationDto>> ToggleVatStatus(
        [FromBody] ToggleVatStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _vatConfigurationService.ToggleVatStatusAsync(request, cancellationToken);
        return Ok(result);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Rates;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/metal-rates")]
[Authorize]
public class MetalRatesController : ControllerBase
{
    private readonly IMetalRateService _metalRateService;

    public MetalRatesController(IMetalRateService metalRateService)
    {
        _metalRateService = metalRateService;
    }

    [HttpGet]
    [RequirePermission(Permissions.RateView)]
    [ProducesResponseType(typeof(List<MetalRateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MetalRateDto>>> GetActiveRates(
        [FromQuery] Guid? branchId,
        [FromQuery] string? currencyCode,
        CancellationToken cancellationToken)
    {
        var result = await _metalRateService.GetActiveRatesAsync(branchId, currencyCode, cancellationToken);
        return Ok(result);
    }

    [HttpGet("active")]
    [RequirePermission(Permissions.RateView)]
    [ProducesResponseType(typeof(MetalRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MetalRateDto>> GetActiveRate(
        [FromQuery] Guid branchId,
        [FromQuery] Guid materialId,
        [FromQuery] Guid purityId,
        [FromQuery] string? currencyCode,
        CancellationToken cancellationToken)
    {
        var result = await _metalRateService.GetActiveRateAsync(branchId, materialId, purityId, currencyCode, cancellationToken);
        return Ok(result);
    }

    [HttpGet("history")]
    [RequirePermission(Permissions.RateView)]
    [ProducesResponseType(typeof(PagedResult<MetalRateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MetalRateDto>>> GetRateHistory(
        [FromQuery] MetalRateHistoryQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        var result = await _metalRateService.GetRateHistoryAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    [HttpPost("manual")]
    [RequirePermission(Permissions.RateManage)]
    [ProducesResponseType(typeof(MetalRateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MetalRateDto>> CreateManualRate(
        [FromBody] CreateManualRateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _metalRateService.CreateManualRateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("override")]
    [RequirePermission(Permissions.RateManage)]
    [ProducesResponseType(typeof(MetalRateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MetalRateDto>> AdminOverrideRate(
        [FromBody] AdminOverrideRateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _metalRateService.AdminOverrideRateAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh-external")]
    [RequirePermission(Permissions.RateManage)]
    [ProducesResponseType(typeof(List<MetalRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<MetalRateDto>>> RefreshExternalRates(
        [FromBody] RefreshExternalRatesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _metalRateService.RefreshExternalRatesAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("provider-status")]
    [RequirePermission(Permissions.RateView)]
    [ProducesResponseType(typeof(ExternalRateProviderStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExternalRateProviderStatusDto>> GetProviderStatus(CancellationToken cancellationToken)
    {
        var result = await _metalRateService.GetProviderStatusAsync(cancellationToken);
        return Ok(result);
    }
}

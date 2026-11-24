using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Rates;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/pricing")]
[Authorize]
public class PricingController : ControllerBase
{
    private readonly IReferencePricingService _pricingService;

    public PricingController(IReferencePricingService pricingService)
    {
        _pricingService = pricingService;
    }

    [HttpPost("calculate-reference")]
    [RequirePermission(Permissions.RateView)]
    [ProducesResponseType(typeof(ReferencePriceBreakdownDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReferencePriceBreakdownDto>> CalculateReferencePrice(
        [FromBody] CalculateReferencePriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _pricingService.CalculateReferencePriceAsync(request, cancellationToken);
        return Ok(result);
    }
}

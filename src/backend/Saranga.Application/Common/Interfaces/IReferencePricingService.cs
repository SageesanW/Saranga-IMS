using Saranga.Application.DTOs.Rates;

namespace Saranga.Application.Common.Interfaces;

public interface IReferencePricingService
{
    Task<ReferencePriceBreakdownDto> CalculateReferencePriceAsync(CalculateReferencePriceRequest request, CancellationToken cancellationToken = default);
}

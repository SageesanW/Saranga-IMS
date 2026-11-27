using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Rates;

namespace Saranga.Application.Common.Interfaces;

public interface IMetalRateService
{
    Task<List<MetalRateDto>> GetActiveRatesAsync(Guid? branchId = null, string? currencyCode = null, CancellationToken cancellationToken = default);
    Task<MetalRateDto> GetActiveRateAsync(Guid branchId, Guid materialId, Guid purityId, string? currencyCode = null, CancellationToken cancellationToken = default);
    Task<PagedResult<MetalRateDto>> GetRateHistoryAsync(MetalRateHistoryQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<MetalRateDto> CreateManualRateAsync(CreateManualRateRequest request, CancellationToken cancellationToken = default);
    Task<MetalRateDto> AdminOverrideRateAsync(AdminOverrideRateRequest request, CancellationToken cancellationToken = default);
    Task<List<MetalRateDto>> RefreshExternalRatesAsync(RefreshExternalRatesRequest request, CancellationToken cancellationToken = default);
    Task<ExternalRateProviderStatusDto> GetProviderStatusAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
    Task<MetalRateFeedPreviewDto> GetFeedPreviewAsync(CancellationToken cancellationToken = default);
    Task<List<MetalRateDto>> SwitchPricingModeAsync(SwitchPricingModeRequest request, CancellationToken cancellationToken = default);
    Task RefreshStaleBranchRatesAsync(CancellationToken cancellationToken = default);
    decimal DerivePurityRate(decimal pureReferenceRate, decimal purityPercentage, decimal? karatValue);
}

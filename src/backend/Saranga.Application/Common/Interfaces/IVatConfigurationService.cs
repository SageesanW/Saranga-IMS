using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Rates;

namespace Saranga.Application.Common.Interfaces;

public interface IVatConfigurationService
{
    Task<VatConfigurationDto> GetActiveVatConfigurationAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<PagedResult<VatConfigurationDto>> GetVatHistoryAsync(VatHistoryQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<VatConfigurationDto> CreateOrUpdateVatConfigurationAsync(CreateVatConfigurationRequest request, CancellationToken cancellationToken = default);
    Task<VatConfigurationDto> ToggleVatStatusAsync(ToggleVatStatusRequest request, CancellationToken cancellationToken = default);
}

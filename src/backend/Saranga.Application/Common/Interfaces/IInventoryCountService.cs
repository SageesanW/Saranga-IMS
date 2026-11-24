using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.InventoryCount;
using Saranga.Domain.Enums;

namespace Saranga.Application.Common.Interfaces;

public interface IInventoryCountService
{
    Task<PagedResult<InventoryCountDto>> GetCountsAsync(InventoryCountQueryParams queryParams, CancellationToken cancellationToken = default);

    Task<InventoryCountDto> GetCountByIdAsync(Guid countId, CancellationToken cancellationToken = default);

    Task<PagedResult<InventoryCountLineDto>> GetCountLinesAsync(Guid countId, bool? isCounted = null, CountLineResult? result = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<List<InventoryCountUnexpectedItemDto>> GetUnexpectedItemsAsync(Guid countId, CancellationToken cancellationToken = default);

    Task<CountVariancesDto> GetCountVariancesAsync(Guid countId, CancellationToken cancellationToken = default);

    Task<InventoryCountDto> StartCountAsync(StartInventoryCountRequest request, CancellationToken cancellationToken = default);

    Task<ScanResultDto> ScanItemAsync(Guid countId, ScanInventoryItemRequest request, CancellationToken cancellationToken = default);

    Task<ConfirmCompositionScanResultDto> ConfirmCompositionScanAsync(Guid countId, ConfirmCompositionScanRequest request, CancellationToken cancellationToken = default);

    Task<InventoryCountDto> FinishCountAsync(Guid countId, CancellationToken cancellationToken = default);

    Task<InventoryCountDto> ResolveVarianceAsync(Guid countId, ResolveVarianceRequest request, CancellationToken cancellationToken = default);

    Task<InventoryCountDto> CompleteCountAsync(Guid countId, CompleteInventoryCountRequest? request = null, CancellationToken cancellationToken = default);

    Task<InventoryCountDto> CancelCountAsync(Guid countId, CancelInventoryCountRequest request, CancellationToken cancellationToken = default);

    Task<InventoryCountActiveCheckDto> CheckActiveCountForLocationAsync(Guid branchId, Guid stockLocationId, CancellationToken cancellationToken = default);
}

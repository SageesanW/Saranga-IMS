using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Shifts;

namespace Saranga.Application.Common.Interfaces;

public interface IShiftService
{
    Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken cancellationToken = default);
    Task<ShiftDto> CloseShiftAsync(Guid shiftId, CloseShiftRequest request, CancellationToken cancellationToken = default);
    Task<ShiftDto> GetShiftByIdAsync(Guid shiftId, CancellationToken cancellationToken = default);
    Task<ShiftDto?> GetActiveShiftForCashierAsync(Guid? cashierId = null, CancellationToken cancellationToken = default);
    Task<ShiftDto?> GetActiveShiftForDrawerAsync(Guid drawerId, CancellationToken cancellationToken = default);
    Task<ShiftOpeningDefaultsDto> GetOpeningDefaultsAsync(Guid drawerId, CancellationToken cancellationToken = default);
    Task<PagedResult<ShiftDto>> GetShiftsAsync(ShiftQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<List<CashDrawerDto>> GetCashDrawersAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<CashDrawerDto> CreateCashDrawerAsync(CreateCashDrawerRequest request, CancellationToken cancellationToken = default);
}

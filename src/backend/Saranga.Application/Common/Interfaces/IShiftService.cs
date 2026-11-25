using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Shifts;
using Saranga.Domain.Entities;

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
    Task<CashDrawerDto> GetCashDrawerByIdAsync(Guid drawerId, CancellationToken cancellationToken = default);
    Task<CashDrawerDto> CreateCashDrawerAsync(CreateCashDrawerRequest request, CancellationToken cancellationToken = default);
    Task<CashDrawerDto> UpdateCashDrawerAsync(Guid drawerId, UpdateCashDrawerRequest request, CancellationToken cancellationToken = default);
    Task<CashDrawerDto> UpdateCashDrawerStatusAsync(Guid drawerId, UpdateCashDrawerStatusRequest request, CancellationToken cancellationToken = default);
    Task DeleteCashDrawerAsync(Guid drawerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transaction-safe lock and retrieval of an active Shift record for Module 12 Cash Ledger coordination.
    /// Uses PostgreSQL row locking to prevent concurrent balance mutations.
    /// </summary>
    Task<Shift> GetShiftForUpdateAsync(Guid shiftId, CancellationToken cancellationToken = default);
}

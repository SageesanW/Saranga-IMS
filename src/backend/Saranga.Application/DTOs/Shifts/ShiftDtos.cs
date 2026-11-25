using Saranga.Domain.Enums;

namespace Saranga.Application.DTOs.Shifts;

public record CashDrawerDto(
    Guid Id,
    Guid BranchId,
    string DrawerCode,
    string Name,
    string? Description,
    bool IsActive,
    bool HasActiveShift = false,
    Guid? ActiveShiftId = null,
    string? ActiveCashierName = null
);

public record CreateCashDrawerRequest(
    Guid BranchId,
    string DrawerCode,
    string Name,
    string? Description = null
);

public record UpdateCashDrawerRequest(
    string DrawerCode,
    string Name,
    string? Description = null,
    Guid? BranchId = null
);

public record UpdateCashDrawerStatusRequest(
    bool IsActive,
    string? Reason = null
);

public record ShiftDto(
    Guid Id,
    string ShiftNumber,
    Guid BranchId,
    string? BranchName,
    Guid CashDrawerId,
    string? CashDrawerName,
    string? CashDrawerCode,
    Guid CashierId,
    string? CashierName,
    string CurrencyCode,
    ShiftStatus Status,
    DateTimeOffset OpenedAt,
    Guid OpenedBy,
    string? OpenedByName,
    decimal OpeningBalance,
    decimal SystemExpectedOpeningBalance,
    decimal OpeningBalanceDifference,
    string? OpeningDifferenceReason,
    string? OpeningNotes,
    DateTimeOffset? ClosedAt,
    Guid? ClosedBy,
    string? ClosedByName,
    decimal? ActualClosingBalance,
    decimal? ExpectedClosingBalance,
    decimal? ClosingVariance,
    string? ClosingVarianceReason,
    string? ClosingNotes,
    bool IsOverrideClose,
    string? OverrideReason,
    uint Version
);

public record OpenShiftRequest(
    Guid BranchId,
    Guid CashDrawerId,
    decimal OpeningBalance,
    string? OpeningDifferenceReason = null,
    string? OpeningNotes = null
);

public record CloseShiftRequest(
    decimal ActualClosingBalance,
    string? ClosingVarianceReason = null,
    string? ClosingNotes = null,
    string? OverrideReason = null,
    uint? RowVersion = null
);

public record ShiftOpeningDefaultsDto(
    Guid CashDrawerId,
    string DrawerName,
    decimal SystemExpectedOpeningBalance,
    DateTimeOffset? PreviousShiftClosedAt,
    string? PreviousShiftNumber
);

public record ShiftQueryParams(
    Guid? BranchId = null,
    Guid? CashDrawerId = null,
    Guid? CashierId = null,
    ShiftStatus? Status = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    int Page = 1,
    int PageSize = 25
);

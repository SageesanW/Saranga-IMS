namespace Saranga.Application.DTOs.Rates;

public record VatConfigurationDto(
    Guid Id,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    string Name,
    bool IsEnabled,
    decimal RatePercentage,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string CalculationMode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? CreatedByName,
    DateTimeOffset? UpdatedAt,
    Guid? UpdatedBy,
    string? UpdatedByName
);

public record CreateVatConfigurationRequest(
    Guid BranchId,
    string Name,
    bool IsEnabled,
    decimal RatePercentage,
    DateTimeOffset? EffectiveFrom = null,
    string? CalculationMode = null
);

public record ToggleVatStatusRequest(
    Guid BranchId,
    bool IsEnabled,
    string? Reason = null
);

public record VatHistoryQueryParams(
    Guid? BranchId = null,
    int Page = 1,
    int PageSize = 50
);

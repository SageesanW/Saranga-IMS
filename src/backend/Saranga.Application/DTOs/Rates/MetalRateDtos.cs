namespace Saranga.Application.DTOs.Rates;

public record MetalRateDto(
    Guid Id,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    Guid PurityId,
    string PurityCode,
    string PurityName,
    decimal PurityKarat,
    decimal PurityPercentage,
    string CurrencyCode,
    decimal RatePerGram,
    string Source,
    decimal? ExternalReferenceRate,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    bool IsActive,
    bool IsOverride,
    string? OverrideReason,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? CreatedByName
);

public record CreateManualRateRequest(
    Guid BranchId,
    Guid MaterialId,
    Guid PurityId,
    decimal RatePerGram,
    string? CurrencyCode = null,
    DateTimeOffset? EffectiveFrom = null,
    string? Notes = null
);

public record AdminOverrideRateRequest(
    Guid BranchId,
    Guid MaterialId,
    Guid PurityId,
    decimal OverrideRatePerGram,
    string OverrideReason,
    string? CurrencyCode = null
);

public record RefreshExternalRatesRequest(
    Guid? BranchId = null,
    string? CurrencyCode = null
);

public record ExternalRateProviderStatusDto(
    string ProviderName,
    bool IsAvailable,
    DateTimeOffset? LastSuccessfulRefreshAt,
    DateTimeOffset? LastAttemptAt,
    string? LastErrorMessage,
    int TotalActiveRatesManaged
);

public record MetalRateHistoryQueryParams(
    Guid? BranchId = null,
    Guid? MaterialId = null,
    Guid? PurityId = null,
    string? CurrencyCode = null,
    int Page = 1,
    int PageSize = 50
);

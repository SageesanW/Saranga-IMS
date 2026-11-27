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
    int TotalActiveRatesManaged,
    string ActivePricingMode = "MANUAL",
    DateTimeOffset? ProviderEffectiveTime = null,
    string? CbslReferenceDate = null,
    string? AttributionText = null,
    string? AttributionUrl = null,
    bool IsStale = false
);

public record SwitchPricingModeRequest(
    Guid BranchId,
    string PricingMode, // "MARKET_API", "CBSL_API", "MANUAL"
    string? Reason = null,
    List<ManualRateConfigItem>? InitialManualRates = null
);

public record ManualRateConfigItem(
    Guid MaterialId,
    Guid PurityId,
    decimal RatePerGram
);

public record FeedPurityRateDto(
    string PurityCode,
    decimal KaratValue,
    decimal RatePerGram,
    decimal RatePerPawn
);

public record MetalRateFeedPreviewDto(
    bool IsSuccess,
    string ProviderName,
    string Currency,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? MarketTime,
    string? CbslDate,
    List<FeedPurityRateDto> MarketRates,
    List<FeedPurityRateDto> CbslRates,
    string AttributionText,
    string AttributionUrl,
    string? ErrorMessage = null
);

public record MetalRateHistoryQueryParams(
    Guid? BranchId = null,
    Guid? MaterialId = null,
    Guid? PurityId = null,
    string? CurrencyCode = null,
    int Page = 1,
    int PageSize = 50
);

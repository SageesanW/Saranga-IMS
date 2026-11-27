namespace Saranga.Application.DTOs.Rates;

public record CalculateReferencePriceRequest(
    Guid BranchId,
    Guid MaterialId,
    Guid PurityId,
    decimal NetWeight,
    int PieceCount,
    int MakingChargeMethod, // 1 = PER_GRAM, 2 = PER_ITEM
    decimal MakingChargeValue,
    string? CurrencyCode = null
);

public record ReferencePriceBreakdownDto(
    Guid BranchId,
    string BranchCode,
    string CurrencyCode,
    Guid MaterialId,
    string MaterialName,
    Guid PurityId,
    string PurityName,
    decimal PurityKarat,
    decimal RatePerGramUsed,
    string RateSource,
    bool IsRateOverridden,
    string? RateOverrideReason,
    decimal NetWeight,
    decimal ReferenceMaterialValue,
    int MakingChargeMethod,
    string MakingChargeMethodName,
    decimal MakingChargeValue,
    int PieceCount,
    decimal MakingChargeAmount,
    decimal SubtotalBeforeTax,
    bool VatEnabled,
    decimal VatRatePercentage,
    decimal VatAmount,
    decimal ReferenceTotal,
    DateTimeOffset CalculatedAt,
    Guid? MetalRateId = null,
    DateTimeOffset? RateEffectiveFrom = null
);

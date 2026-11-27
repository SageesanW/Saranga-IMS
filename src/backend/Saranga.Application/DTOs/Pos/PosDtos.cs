using Saranga.Domain.Enums;

namespace Saranga.Application.DTOs.Pos;

public record PosItemLookupResultDto(
    Guid InventoryItemId,
    string ItemCode,
    string TagNumber,
    string? Barcode,
    string ItemName,
    Guid CategoryId,
    string CategoryName,
    Guid MaterialId,
    string MaterialName,
    Guid PurityId,
    string PurityName,
    decimal KaratValue,
    decimal GrossWeight,
    decimal StoneWeight,
    decimal NetWeight,
    int PieceCount,
    decimal ActiveRatePerGram,
    decimal ReferenceMaterialValue,
    decimal ReferenceMakingCharge,
    decimal ReferencePrice,
    string CurrencyCode,
    bool IsPartOfComposition,
    Guid? CompositionId,
    string? CompositionName,
    string? CompositionTagNumber,
    string? CompositionType,
    string? PhotographUrl = null,
    string RateSource = "",
    Guid? MetalRateId = null,
    DateTimeOffset? RateEffectiveFrom = null,
    DateTimeOffset? QuotedAt = null,
    int MakingChargeMethod = 1,
    string MakingChargeMethodName = "PER_GRAM",
    decimal MakingChargeValue = 0m,
    decimal GoldMaterialValue = 0m,
    decimal TotalMakingCharge = 0m,
    decimal ExpectedSellingPrice = 0m
);

public record PosInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid BranchId,
    string? BranchName,
    Guid ShiftId,
    string? ShiftNumber,
    Guid CashierId,
    string? CashierName,
    Guid CustomerId,
    string? CustomerCode,
    string CustomerName,
    string CustomerPhone,
    int Status,
    string StatusName,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal VatRate,
    decimal VatAmount,
    decimal TotalAmount,
    decimal TotalPaidAmount,
    decimal TotalCashTendered,
    decimal TotalCashChange,
    decimal NetCashPaid,
    string CurrencyCode,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinalizedAt,
    List<PosInvoiceLineDto> Lines,
    List<PosInvoicePaymentDto> Payments,
    decimal CombinedExpectedTotal = 0m,
    decimal CombinedAgreedTotal = 0m,
    decimal ItemLevelPriceDifference = 0m,
    decimal InvoiceAdjustmentAmount = 0m,
    decimal FinalNegotiatedInvoicePrice = 0m,
    bool IsInvoicePriceConfirmed = false,
    bool HasRateUpdates = false
);

public record PosInvoiceLineDto(
    Guid Id,
    int LineType,
    string LineTypeName,
    Guid? InventoryItemId,
    Guid? CompositionId,
    string ItemCode,
    string TagNumber,
    string? Barcode,
    string ItemName,
    decimal GrossWeight,
    decimal StoneWeight,
    decimal NetWeight,
    int PieceCount,
    string? MaterialName,
    decimal? KaratValue,
    decimal ReferenceRatePerGram,
    decimal ReferenceMaterialValue,
    decimal ReferenceMakingCharge,
    decimal ReferencePrice,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    string? Notes,
    Guid? MetalRateId = null,
    string? RateSource = null,
    DateTimeOffset? RateEffectiveAt = null,
    DateTimeOffset? QuotedAt = null,
    int MakingChargeMethod = 1,
    string MakingChargeMethodName = "PER_GRAM",
    decimal MakingChargeValue = 0m,
    decimal ItemPriceDifference = 0m,
    decimal AllocatedInvoiceAdjustment = 0m,
    decimal NetLineTotal = 0m,
    string? PhotographUrl = null
);

public record PosInvoicePaymentDto(
    Guid Id,
    int PaymentMethod,
    string PaymentMethodName,
    decimal Amount,
    decimal? CashTendered,
    decimal? ChangeGiven,
    string? ReferenceNumber,
    string? CardType,
    string? CardLast4,
    string? BankName,
    Guid? CashLedgerTransactionId,
    string? Notes,
    DateTimeOffset CreatedAt
);

public record CreatePosDraftInvoiceRequest(
    Guid ShiftId,
    Guid CustomerId,
    string? Notes
);

public record QuickCreateCustomerAndDraftRequest(
    Guid ShiftId,
    string FullName,
    string PrimaryPhone,
    string? Email,
    string? AddressLine1,
    string? City,
    string? Notes
);

public record AddPosInvoiceLineRequest(
    string ItemBarcodeOrTag,
    decimal? NegotiatedUnitPrice = null,
    decimal? AgreedSellingPrice = null,
    string? Notes = null
);

public record UpdatePosInvoiceLineRequest(
    decimal? NegotiatedUnitPrice = null,
    decimal? AgreedSellingPrice = null,
    string? Notes = null
);

public record ConfirmNegotiatedInvoiceTotalRequest(
    decimal FinalNegotiatedTotal,
    string? Notes = null
);

public record CheckInvoiceRatesStatusResponse(
    bool HasRateUpdates,
    List<string> ChangedPurities,
    string Message
);

public record RefreshInvoiceReferenceRatesResponse(
    PosInvoiceDto Invoice,
    int LinesRefreshedCount,
    string Message
);

public record FinalizePosInvoiceRequest(
    List<PosPaymentRequest> Payments,
    decimal? DiscountAmount,
    string? Notes
);

public record PosPaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    decimal? CashTendered,
    string? ReferenceNumber,
    string? CardType,
    string? CardLast4,
    string? BankName,
    string? Notes
);

public record PosInvoiceFilter(
    Guid? BranchId,
    Guid? CustomerId,
    Guid? CashierId,
    Guid? ShiftId,
    InvoiceStatus? Status,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Page = 1,
    int PageSize = 20
);

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
    string? CompositionType
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
    List<PosInvoicePaymentDto> Payments
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
    string? Notes
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
    decimal? NegotiatedUnitPrice,
    string? Notes
);

public record UpdatePosInvoiceLineRequest(
    decimal NegotiatedUnitPrice,
    string? Notes
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

using Saranga.Application.DTOs.Common;

namespace Saranga.Application.DTOs.CashLedger;

public record CashMovementCategoryDto(
    Guid Id,
    string Name,
    string Code,
    int AllowedDirection,
    string? Description,
    bool IsActive,
    bool IsSystem,
    DateTimeOffset CreatedAt
);

public record CreateCashMovementCategoryRequest(
    string Name,
    string Code,
    int AllowedDirection,
    string? Description
);

public record UpdateCashMovementCategoryRequest(
    string Name,
    int AllowedDirection,
    string? Description
);

public record UpdateCategoryStatusRequest(
    bool IsActive
);

public record CashLedgerEntryDto(
    Guid Id,
    Guid CashLedgerTransactionId,
    Guid BranchId,
    Guid CashDrawerId,
    string? CashDrawerName,
    string? CashDrawerCode,
    Guid ShiftId,
    string? ShiftNumber,
    int Direction,
    string DirectionName,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? CreatedByName
);

public record CashLedgerTransactionDto(
    Guid Id,
    string TransactionReference,
    Guid BranchId,
    string? BranchName,
    int TransactionType,
    string TransactionTypeName,
    int SourceType,
    string SourceTypeName,
    string? SourceReferenceId,
    decimal Amount,
    string CurrencyCode,
    string? Description,
    string? Reason,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryCode,
    int Status,
    string StatusName,
    DateTimeOffset TransactionAt,
    Guid? ReversalOfTransactionId,
    string? ReversalOfTransactionReference,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? CreatedByName,
    List<CashLedgerEntryDto> Entries
);

public record PostCashInRequest(
    Guid ShiftId,
    decimal Amount,
    Guid? CategoryId,
    string? Description,
    string? Reason,
    string? ExternalReference
);

public record PostCashOutRequest(
    Guid ShiftId,
    decimal Amount,
    Guid? CategoryId,
    string? Description,
    string? Reason,
    string? ExternalReference
);

public record PostTransferRequest(
    Guid SourceShiftId,
    Guid DestinationShiftId,
    decimal Amount,
    string Reason,
    string? ExternalReference
);

public record ReverseTransactionRequest(
    string ReversalReason
);

public record CashLedgerTransactionFilter(
    Guid? BranchId = null,
    Guid? CashDrawerId = null,
    Guid? ShiftId = null,
    int? TransactionType = null,
    Guid? CategoryId = null,
    Guid? UserId = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int? Status = null,
    int Page = 1,
    int PageSize = 50
);

public record ShiftCashLedgerSummaryDto(
    Guid ShiftId,
    string ShiftNumber,
    Guid BranchId,
    string? BranchName,
    Guid CashDrawerId,
    string? CashDrawerName,
    string? CashDrawerCode,
    Guid CashierId,
    string? CashierName,
    int ShiftStatus,
    decimal OpeningBalance,
    decimal TotalCashIn,
    decimal TotalCashOut,
    decimal CurrentExpectedBalance,
    string CurrencyCode,
    int TotalTransactionsCount
);

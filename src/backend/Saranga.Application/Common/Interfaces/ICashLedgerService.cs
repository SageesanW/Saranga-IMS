using Saranga.Application.DTOs.CashLedger;
using Saranga.Application.DTOs.Common;
using Saranga.Domain.Enums;

namespace Saranga.Application.Common.Interfaces;

public interface ICashLedgerService
{
    Task<PagedResult<CashLedgerTransactionDto>> GetTransactionsAsync(CashLedgerTransactionFilter filter, CancellationToken cancellationToken = default);
    Task<CashLedgerTransactionDto> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CashLedgerTransactionDto> PostCashInAsync(PostCashInRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<CashLedgerTransactionDto> PostCashOutAsync(PostCashOutRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<CashLedgerTransactionDto> PostTransferAsync(PostTransferRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<CashLedgerTransactionDto> ReverseTransactionAsync(Guid transactionId, ReverseTransactionRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    Task<ShiftCashLedgerSummaryDto> GetShiftSummaryAsync(Guid shiftId, CancellationToken cancellationToken = default);

    Task<List<CashMovementCategoryDto>> GetCategoriesAsync(CashCategoryDirection? direction = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CashMovementCategoryDto> CreateCategoryAsync(CreateCashMovementCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CashMovementCategoryDto> UpdateCategoryAsync(Guid id, UpdateCashMovementCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CashMovementCategoryDto> UpdateCategoryStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    // Reusable Application-layer operations for future POS sales and refunds (Section 33)
    Task<CashLedgerTransactionDto> PostCashPaymentAsync(CashLedgerSourceType sourceType, string sourceReferenceId, Guid shiftId, decimal amount, string currencyCode, string? notes, CancellationToken cancellationToken = default);
    Task<CashLedgerTransactionDto> PostCashRefundAsync(CashLedgerSourceType sourceType, string sourceReferenceId, Guid shiftId, decimal amount, string currencyCode, string? notes, CancellationToken cancellationToken = default);
}

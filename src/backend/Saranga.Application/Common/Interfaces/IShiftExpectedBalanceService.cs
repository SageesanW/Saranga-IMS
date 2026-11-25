using Saranga.Application.DTOs.CashLedger;

namespace Saranga.Application.Common.Interfaces;

public interface IShiftExpectedBalanceService
{
    /// <summary>
    /// Calculates the authoritative expected cash closing balance for a shift based on posted cash ledger entries.
    /// Expected Cash Balance = Opening Balance + Total Posted Cash In - Total Posted Cash Out.
    /// </summary>
    Task<decimal> CalculateExpectedClosingBalanceAsync(Guid shiftId, decimal openingBalance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a complete physical cash movement summary for a shift.
    /// </summary>
    Task<ShiftCashLedgerSummaryDto> GetShiftCashSummaryAsync(Guid shiftId, CancellationToken cancellationToken = default);
}

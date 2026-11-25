namespace Saranga.Application.Common.Interfaces;

public interface IShiftExpectedBalanceService
{
    /// <summary>
    /// Calculates the authoritative expected cash closing balance for a shift.
    /// In Module 11: Expected Closing Balance = Opening Balance (no POS / Cash movements yet).
    /// In Module 12 & 13: Expected = Opening + CashIn - CashOut + PosCashSales - PosCashReturns.
    /// </summary>
    Task<decimal> CalculateExpectedClosingBalanceAsync(Guid shiftId, decimal openingBalance, CancellationToken cancellationToken = default);
}

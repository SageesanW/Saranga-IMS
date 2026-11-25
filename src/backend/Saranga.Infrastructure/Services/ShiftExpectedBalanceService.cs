using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.CashLedger;
using Saranga.Domain.Enums;

namespace Saranga.Infrastructure.Services;

public class ShiftExpectedBalanceService : IShiftExpectedBalanceService
{
    private readonly IAppDbContext _context;

    public ShiftExpectedBalanceService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> CalculateExpectedClosingBalanceAsync(Guid shiftId, decimal openingBalance, CancellationToken cancellationToken = default)
    {
        var inSum = await _context.CashLedgerEntries
            .Where(e => e.ShiftId == shiftId && e.Direction == CashLedgerEntryDirection.In && !e.IsDeleted)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var outSum = await _context.CashLedgerEntries
            .Where(e => e.ShiftId == shiftId && e.Direction == CashLedgerEntryDirection.Out && !e.IsDeleted)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        return openingBalance + inSum - outSum;
    }

    public async Task<ShiftCashLedgerSummaryDto> GetShiftCashSummaryAsync(Guid shiftId, CancellationToken cancellationToken = default)
    {
        var shift = await _context.Shifts
            .Include(s => s.Branch)
            .Include(s => s.CashDrawer)
            .Include(s => s.Cashier)
            .FirstOrDefaultAsync(s => s.Id == shiftId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Shift", shiftId);

        var inSum = await _context.CashLedgerEntries
            .Where(e => e.ShiftId == shiftId && e.Direction == CashLedgerEntryDirection.In && !e.IsDeleted)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var outSum = await _context.CashLedgerEntries
            .Where(e => e.ShiftId == shiftId && e.Direction == CashLedgerEntryDirection.Out && !e.IsDeleted)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var txCount = await _context.CashLedgerEntries
            .Where(e => e.ShiftId == shiftId && !e.IsDeleted)
            .Select(e => e.CashLedgerTransactionId)
            .Distinct()
            .CountAsync(cancellationToken);

        // For closed shift, expected balance is the persisted historical closing figure
        var expectedBalance = shift.Status == ShiftStatus.Closed && shift.ExpectedClosingBalance.HasValue
            ? shift.ExpectedClosingBalance.Value
            : shift.OpeningBalance + inSum - outSum;

        return new ShiftCashLedgerSummaryDto(
            shift.Id,
            shift.ShiftNumber,
            shift.BranchId,
            shift.Branch?.Name,
            shift.CashDrawerId,
            shift.CashDrawer?.Name,
            shift.CashDrawer?.DrawerCode,
            shift.CashierId,
            shift.Cashier?.FullName,
            (int)shift.Status,
            shift.OpeningBalance,
            inSum,
            outSum,
            expectedBalance,
            shift.CurrencyCode,
            txCount
        );
    }
}

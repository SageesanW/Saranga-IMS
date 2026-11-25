using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class CashLedgerEntry : BaseEntity
{
    public Guid CashLedgerTransactionId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CashDrawerId { get; set; }
    public Guid ShiftId { get; set; }
    public CashLedgerEntryDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "LKR";

    // Navigations
    public CashLedgerTransaction? CashLedgerTransaction { get; set; }
    public Branch? Branch { get; set; }
    public CashDrawer? CashDrawer { get; set; }
    public Shift? Shift { get; set; }
    public User? Creator { get; set; }
}

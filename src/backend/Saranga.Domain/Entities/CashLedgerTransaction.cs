using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class CashLedgerTransaction : BaseEntity
{
    public string TransactionReference { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public CashLedgerTransactionType TransactionType { get; set; }
    public CashLedgerSourceType SourceType { get; set; } = CashLedgerSourceType.Manual;
    public string? SourceReferenceId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "LKR";
    public string? Description { get; set; }
    public string? Reason { get; set; }
    public Guid? CategoryId { get; set; }
    public CashLedgerStatus Status { get; set; } = CashLedgerStatus.Posted;
    public DateTimeOffset TransactionAt { get; set; }
    public Guid? ReversalOfTransactionId { get; set; }
    public string? IdempotencyKey { get; set; }

    // Navigations
    public Branch? Branch { get; set; }
    public CashMovementCategory? Category { get; set; }
    public CashLedgerTransaction? ReversalOfTransaction { get; set; }
    public ICollection<CashLedgerTransaction> ReversedByTransactions { get; set; } = new List<CashLedgerTransaction>();
    public ICollection<CashLedgerEntry> Entries { get; set; } = new List<CashLedgerEntry>();
    public User? Creator { get; set; }
    public User? Updater { get; set; }
}

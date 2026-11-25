namespace Saranga.Domain.Enums;

public enum CashLedgerTransactionType
{
    CashIn = 1,
    CashOut = 2,
    Transfer = 3,
    Reversal = 4
}

public enum CashLedgerSourceType
{
    Manual = 1,
    PosSale = 2,
    PosRefund = 3,
    RepairPayment = 4,
    OrderPayment = 5,
    OldGoldPayment = 6,
    SystemReversal = 7
}

public enum CashLedgerStatus
{
    Posted = 1,
    Reversed = 2
}

public enum CashLedgerEntryDirection
{
    In = 1,
    Out = 2
}

public enum CashCategoryDirection
{
    In = 1,
    Out = 2,
    Both = 3
}

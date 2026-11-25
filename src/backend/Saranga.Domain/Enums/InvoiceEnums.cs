namespace Saranga.Domain.Enums;

public enum InvoiceStatus
{
    Draft = 0,
    Finalized = 1,
    Cancelled = 2,
    Refunded = 3
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3
}

public enum InvoiceLineType
{
    InventoryItem = 1,
    Assembly = 2,
    Group = 3
}

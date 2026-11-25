using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InvoicePayment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public decimal? CashTendered { get; set; }
    public decimal? ChangeGiven { get; set; }

    public string? ReferenceNumber { get; set; }
    public string? CardType { get; set; }
    public string? CardLast4 { get; set; }
    public string? BankName { get; set; }

    public Guid? CashLedgerTransactionId { get; set; }
    public string? Notes { get; set; }

    // Navigations
    public Invoice? Invoice { get; set; }
    public CashLedgerTransaction? CashLedgerTransaction { get; set; }
}

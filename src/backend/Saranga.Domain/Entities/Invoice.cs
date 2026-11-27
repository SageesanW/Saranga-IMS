using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid CashierId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerPhoneSnapshot { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    // Module 13: Negotiated Pricing Architecture
    public decimal CombinedExpectedTotal { get; set; }
    public decimal CombinedAgreedTotal { get; set; }
    public decimal InvoiceAdjustmentAmount { get; set; }
    public bool IsInvoicePriceConfirmed { get; set; }

    public decimal TotalPaidAmount { get; set; }
    public decimal TotalCashTendered { get; set; }
    public decimal TotalCashChange { get; set; }
    public decimal NetCashPaid { get; set; }

    public string CurrencyCode { get; set; } = "LKR";
    public string? Notes { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public string? IdempotencyKey { get; set; }

    // Navigations
    public Branch? Branch { get; set; }
    public Shift? Shift { get; set; }
    public User? Cashier { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
}

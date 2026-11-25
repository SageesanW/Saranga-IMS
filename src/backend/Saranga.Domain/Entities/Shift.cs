using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class Shift : BaseEntity
{
    public string ShiftNumber { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    public Guid CashDrawerId { get; set; }

    public Guid CashierId { get; set; }

    public string CurrencyCode { get; set; } = "LKR";

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public DateTimeOffset OpenedAt { get; set; }

    public Guid OpenedBy { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal SystemExpectedOpeningBalance { get; set; }

    public decimal OpeningBalanceDifference { get; set; }

    public string? OpeningDifferenceReason { get; set; }

    public string? OpeningNotes { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public Guid? ClosedBy { get; set; }

    public decimal? ActualClosingBalance { get; set; }

    public decimal? ExpectedClosingBalance { get; set; }

    public decimal? ClosingVariance { get; set; }

    public string? ClosingVarianceReason { get; set; }

    public string? ClosingNotes { get; set; }

    public bool IsOverrideClose { get; set; } = false;

    public string? OverrideReason { get; set; }

    public uint Version { get; set; }

    // Navigations
    public Branch? Branch { get; set; }
    public CashDrawer? CashDrawer { get; set; }
    public User? Cashier { get; set; }
    public User? Opener { get; set; }
    public User? Closer { get; set; }
}

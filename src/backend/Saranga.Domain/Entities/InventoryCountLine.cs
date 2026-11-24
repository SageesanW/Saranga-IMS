using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InventoryCountLine : BaseEntity
{
    public Guid InventoryCountId { get; set; }
    public InventoryCount? InventoryCount { get; set; }

    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public Guid ExpectedBranchId { get; set; }
    public Branch? ExpectedBranch { get; set; }

    public Guid ExpectedStockLocationId { get; set; }
    public StockLocation? ExpectedStockLocation { get; set; }

    // Historical Frozen Baseline Snapshots
    public string TagNumberSnapshot { get; set; } = string.Empty;
    public string BarcodeSnapshot { get; set; } = string.Empty;
    public string ItemNameSnapshot { get; set; } = string.Empty;
    public string MaterialNameSnapshot { get; set; } = string.Empty;
    public string CategoryNameSnapshot { get; set; } = string.Empty;
    public string PurityNameSnapshot { get; set; } = string.Empty;

    public decimal GrossWeightSnapshot { get; set; }
    public decimal StoneWeightSnapshot { get; set; }
    public decimal NetWeightSnapshot { get; set; }
    public int PieceCountSnapshot { get; set; }
    public string ExpectedStatusSnapshot { get; set; } = string.Empty;

    // Count Progress State
    public bool IsExpected { get; set; } = true;
    public bool IsCounted { get; set; } = false;
    public DateTimeOffset? CountedAt { get; set; }
    public Guid? CountedBy { get; set; }
    public CountLineResult Result { get; set; } = CountLineResult.Pending;

    // Variance Resolution State
    public VarianceResolutionStatus ResolutionStatus { get; set; } = VarianceResolutionStatus.Unresolved;
    public string? ResolutionReasonCode { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }

    public bool IsActive { get; set; } = true;
}

using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InventoryCountUnexpectedItem : BaseEntity
{
    public Guid InventoryCountId { get; set; }
    public InventoryCount? InventoryCount { get; set; }

    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public Guid ObservedBranchId { get; set; }
    public Branch? ObservedBranch { get; set; }

    public Guid ObservedStockLocationId { get; set; }
    public StockLocation? ObservedStockLocation { get; set; }

    public Guid CurrentSystemStockLocationId { get; set; }
    public StockLocation? CurrentSystemStockLocation { get; set; }

    // Frozen Observation Snapshots
    public string TagNumberSnapshot { get; set; } = string.Empty;
    public string BarcodeSnapshot { get; set; } = string.Empty;
    public string ItemNameSnapshot { get; set; } = string.Empty;
    public string MaterialNameSnapshot { get; set; } = string.Empty;
    public string CategoryNameSnapshot { get; set; } = string.Empty;
    public string PurityNameSnapshot { get; set; } = string.Empty;
    public string CurrentSystemLocationNameSnapshot { get; set; } = string.Empty;

    public decimal GrossWeightSnapshot { get; set; }
    public decimal StoneWeightSnapshot { get; set; }
    public decimal NetWeightSnapshot { get; set; }
    public int PieceCountSnapshot { get; set; }
    public string CurrentSystemStatusSnapshot { get; set; } = string.Empty;

    // Observation Tracking
    public DateTimeOffset ScannedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ScannedBy { get; set; }

    // Variance Resolution State
    public VarianceResolutionStatus ResolutionStatus { get; set; } = VarianceResolutionStatus.Unresolved;
    public string? ResolutionReasonCode { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }

    public bool IsActive { get; set; } = true;
}

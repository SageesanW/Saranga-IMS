using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InventoryCount : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid StockLocationId { get; set; }
    public StockLocation? StockLocation { get; set; }

    public InventoryCountStatus Status { get; set; } = InventoryCountStatus.InProgress;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid StartedBy { get; set; }

    public DateTimeOffset? ReconciledAt { get; set; }
    public Guid? ReconciledBy { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedBy { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }

    public int ExpectedItemCount { get; set; }
    public int CountedExpectedItemCount { get; set; }
    public int MatchedCount { get; set; }
    public int MissingCount { get; set; }
    public int UnexpectedCount { get; set; }

    public string? Notes { get; set; }

    // PostgreSQL MVCC concurrency token for optimistic locking
    public uint Version { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<InventoryCountLine> Lines { get; set; } = new List<InventoryCountLine>();
    public ICollection<InventoryCountUnexpectedItem> UnexpectedItems { get; set; } = new List<InventoryCountUnexpectedItem>();
}

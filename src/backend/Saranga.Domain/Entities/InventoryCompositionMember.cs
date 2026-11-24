using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class InventoryCompositionMember : BaseEntity
{
    public Guid CompositionId { get; set; }
    public InventoryComposition? Composition { get; set; }

    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? AddedBy { get; set; }

    public DateTimeOffset? RemovedAt { get; set; }
    public Guid? RemovedBy { get; set; }
}

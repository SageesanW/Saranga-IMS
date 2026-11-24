using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InventoryComposition : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid StockLocationId { get; set; }
    public StockLocation? StockLocation { get; set; }

    public string Name { get; set; } = string.Empty;
    public CompositionType Type { get; set; } = CompositionType.Assembly;
    public string TagNumber { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public CompositionStatus Status { get; set; } = CompositionStatus.Active;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // PostgreSQL MVCC concurrency token for optimistic locking
    public uint Version { get; set; }

    public ICollection<InventoryCompositionMember> Members { get; set; } = new List<InventoryCompositionMember>();
}

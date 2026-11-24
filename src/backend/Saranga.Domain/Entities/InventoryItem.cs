using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InventoryItem : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid StockLocationId { get; set; }
    public StockLocation? StockLocation { get; set; }

    public string Name { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty; // SKU / Code
    public string TagNumber { get; set; } = string.Empty; // Unique jewellery tag
    public string Barcode { get; set; } = string.Empty; // Machine-scannable barcode

    public Guid MaterialId { get; set; }
    public Material? Material { get; set; }

    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid PurityId { get; set; }
    public Purity? Purity { get; set; }

    public decimal GrossWeight { get; set; } // numeric(18,4)
    public decimal StoneWeight { get; set; } // numeric(18,4)
    public decimal NetWeight { get; set; } // numeric(18,4)

    public int PieceCount { get; set; } = 1;

    public MakingChargeMethod MakingChargeMethod { get; set; } = MakingChargeMethod.PerGram;
    public decimal MakingChargeValue { get; set; } = 0.00m; // numeric(18,2)

    public InventoryStatus Status { get; set; } = InventoryStatus.Available;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // PostgreSQL MVCC concurrency token for optimistic locking (prevents concurrent double-sale in POS)
    public uint Version { get; set; }

    public ICollection<InventoryItemSpecification> Specifications { get; set; } = new List<InventoryItemSpecification>();
    public ICollection<InventoryItemMedia> Media { get; set; } = new List<InventoryItemMedia>();
}

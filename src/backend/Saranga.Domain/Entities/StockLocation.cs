using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class StockLocation : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Name { get; set; } = string.Empty; // e.g. "Main Display", "Cabinet A", "Vault"
    public string Code { get; set; } = string.Empty; // e.g. "DISP-01", "CAB-A", "VLT-01"
    public string Description { get; set; } = string.Empty;
    public string LocationType { get; set; } = "Display"; // "Display", "Vault", "Storage", "Counter"
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

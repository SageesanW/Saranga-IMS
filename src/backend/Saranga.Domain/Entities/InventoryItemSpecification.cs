using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class InventoryItemSpecification : BaseEntity
{
    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public Guid SpecificationId { get; set; }
    public Specification? Specification { get; set; }

    public Guid? SpecificationOptionId { get; set; }
    public SpecificationOption? SpecificationOption { get; set; }

    public string? CustomValue { get; set; }
}

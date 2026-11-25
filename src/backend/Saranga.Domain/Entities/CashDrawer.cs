using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class CashDrawer : BaseEntity
{
    public Guid BranchId { get; set; }

    public string DrawerCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigations
    public Branch? Branch { get; set; }
}

using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class Permission : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g. POS_VIEW, POS_CREATE_SALE
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty; // e.g. POS, Inventory, Cash, Verification

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

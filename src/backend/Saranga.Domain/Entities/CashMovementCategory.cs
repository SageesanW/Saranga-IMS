using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class CashMovementCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public CashCategoryDirection AllowedDirection { get; set; } = CashCategoryDirection.Both;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; } = false;

    // Navigations
    public User? Creator { get; set; }
    public User? Updater { get; set; }
    public ICollection<CashLedgerTransaction> Transactions { get; set; } = new List<CashLedgerTransaction>();
}

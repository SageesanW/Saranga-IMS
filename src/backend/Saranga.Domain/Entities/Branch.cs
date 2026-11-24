using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class Branch : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // e.g. "JAF-01", "CAN-01"
    public string Country { get; set; } = "Sri Lanka";
    public string CurrencyCode { get; set; } = "LKR";
    public string TimeZone { get; set; } = "Asia/Colombo";
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<User> DefaultUsers { get; set; } = new List<User>();
    public ICollection<UserBranch> UserBranches { get; set; } = new List<UserBranch>();
    public ICollection<StockLocation> StockLocations { get; set; } = new List<StockLocation>();
}

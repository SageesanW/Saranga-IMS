using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class Purity : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g. "24K", "22K"
    public string Code { get; set; } = string.Empty; // e.g. "24K", "22K"
    public decimal KaratValue { get; set; } // e.g. 24.0, 22.0, 18.0
    public decimal PurityPercentage { get; set; } // Fineness percentage e.g. 99.90, 91.67
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

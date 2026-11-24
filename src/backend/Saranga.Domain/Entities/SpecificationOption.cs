using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class SpecificationOption : BaseEntity
{
    public Guid SpecificationId { get; set; }
    public Specification? Specification { get; set; }

    public string Value { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

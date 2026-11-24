using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class Specification : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InputType { get; set; } = "PREDEFINED";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public ICollection<SpecificationOption> Options { get; set; } = new List<SpecificationOption>();
}

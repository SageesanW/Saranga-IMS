namespace Saranga.Domain.Common;

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

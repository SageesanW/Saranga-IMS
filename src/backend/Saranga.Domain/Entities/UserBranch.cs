namespace Saranga.Domain.Entities;

public class UserBranch
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsDefault { get; set; } = false;
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

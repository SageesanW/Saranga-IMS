namespace Saranga.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // e.g. CREATE, UPDATE, DELETE, LOGIN, LOGOUT
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public Guid? BranchId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? OldValues { get; set; } // JSON format
    public string? NewValues { get; set; } // JSON format
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class IdempotencyRecord : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
}

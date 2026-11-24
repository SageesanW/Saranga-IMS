namespace Saranga.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    Guid? BranchId { get; }
    string? Role { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

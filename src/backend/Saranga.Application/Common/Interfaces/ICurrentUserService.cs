namespace Saranga.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    Guid? BranchId { get; }
    string? Role { get; }
    bool IsGlobalAdmin { get; }
    IReadOnlyList<Guid> AccessibleBranchIds { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

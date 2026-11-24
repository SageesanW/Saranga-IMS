using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Default or active branch for this user. Multi-branch access is governed by UserBranches.
    /// </summary>
    public Guid? DefaultBranchId { get; set; }
    public Branch? DefaultBranch { get; set; }

    /// <summary>
    /// Indicates whether the user has unrestricted business-wide oversight (Owner / Super Admin).
    /// </summary>
    public bool IsGlobalAdmin { get; set; } = false;

    // RBAC Navigation collections
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserBranch> UserBranches { get; set; } = new List<UserBranch>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

namespace Saranga.Application.DTOs.Users;

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    List<string> Roles,
    List<string> Permissions,
    List<Guid> BranchIds,
    Guid? DefaultBranchId,
    bool IsGlobalAdmin,
    bool IsActive,
    DateTimeOffset? LastLoginAt = null,
    DateTimeOffset CreatedAt = default
);

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string FullName,
    List<Guid> RoleIds,
    List<Guid> BranchIds,
    Guid? DefaultBranchId,
    bool IsGlobalAdmin = false
);

public record UpdateUserRequest(
    string Email,
    string FullName,
    Guid? DefaultBranchId
);

public record AssignUserRolesRequest(
    List<Guid> RoleIds
);

public record AssignUserBranchesRequest(
    List<Guid> BranchIds,
    Guid? DefaultBranchId
);

public record ResetUserPasswordRequest(
    string NewPassword
);

public record UserFilterRequest(
    string? Search = null,
    Guid? BranchId = null,
    Guid? RoleId = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);

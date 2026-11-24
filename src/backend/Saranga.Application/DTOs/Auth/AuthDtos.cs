namespace Saranga.Application.DTOs.Auth;

public record LoginRequest(string Username, string Password);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<Guid> BranchIds,
    Guid? DefaultBranchId,
    bool IsGlobalAdmin,
    bool IsActive
);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserDto User
);

public record BootstrapOwnerRequest(
    string Username,
    string Email,
    string Password,
    string FullName
);

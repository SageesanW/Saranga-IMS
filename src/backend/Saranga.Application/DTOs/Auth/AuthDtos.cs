using Saranga.Application.DTOs.Users;

namespace Saranga.Application.DTOs.Auth;

public record LoginRequest(string Username, string Password);

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

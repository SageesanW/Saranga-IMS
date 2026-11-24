using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Entities;

namespace Saranga.Application.Services;

public interface IAuthService
{
    Task<(AuthResponse Response, string RawRefreshToken)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<(AuthResponse Response, string NewRawRefreshToken)> RefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
    Task<UserDto> BootstrapInitialOwnerAsync(BootstrapOwnerRequest request, CancellationToken cancellationToken = default);
    Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private readonly IAppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUserService;

    public AuthService(
        IAppDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _currentUserService = currentUserService;
    }

    public async Task<(AuthResponse Response, string RawRefreshToken)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower(), cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User account is inactive. Please contact your manager.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;

        var roles = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Name)
            .Distinct()
            .ToList();

        var permissions = user.UserRoles
            .Where(ur => ur.Role != null)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.Name)
            .Distinct()
            .ToList();

        var branchIds = user.UserBranches.Select(ub => ub.BranchId).ToList();

        var (rawRefreshToken, refreshTokenEntity) = _jwtTokenService.GenerateRefreshToken(user.Id);
        _context.RefreshTokens.Add(refreshTokenEntity);

        var accessToken = _jwtTokenService.GenerateAccessToken(user, roles, permissions, branchIds);

        // Record audit log for login (no sensitive tokens/passwords stored)
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "LOGIN",
            UserId = user.Id,
            Username = user.Username,
            BranchId = user.DefaultBranchId,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            roles,
            permissions,
            branchIds,
            user.DefaultBranchId,
            user.IsGlobalAdmin,
            user.IsActive,
            user.LastLoginAt,
            user.CreatedAt
        );

        var response = new AuthResponse(
            accessToken,
            refreshTokenEntity.ExpiresAt,
            userDto
        );

        return (response, rawRefreshToken);
    }

    public async Task<(AuthResponse Response, string NewRawRefreshToken)> RefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtTokenService.HashToken(rawRefreshToken);

        var existingToken = await _context.RefreshTokens
            .Include(r => r.User)
                .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r!.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
            .Include(r => r.User)
                .ThenInclude(u => u!.UserBranches)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

        if (existingToken == null || !existingToken.IsActive || existingToken.User == null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token session.");
        }

        var user = existingToken.User;
        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User account is inactive.");
        }

        // Revoke current token and rotate with new token
        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        var (newRawRefreshToken, newRefreshTokenEntity) = _jwtTokenService.GenerateRefreshToken(user.Id);
        existingToken.ReplacedByTokenHash = newRefreshTokenEntity.TokenHash;

        _context.RefreshTokens.Add(newRefreshTokenEntity);

        var roles = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Name)
            .Distinct()
            .ToList();

        var permissions = user.UserRoles
            .Where(ur => ur.Role != null)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.Name)
            .Distinct()
            .ToList();

        var branchIds = user.UserBranches.Select(ub => ub.BranchId).ToList();
        var newAccessToken = _jwtTokenService.GenerateAccessToken(user, roles, permissions, branchIds);

        await _context.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            roles,
            permissions,
            branchIds,
            user.DefaultBranchId,
            user.IsGlobalAdmin,
            user.IsActive,
            user.LastLoginAt,
            user.CreatedAt
        );

        var response = new AuthResponse(
            newAccessToken,
            newRefreshTokenEntity.ExpiresAt,
            userDto
        );

        return (response, newRawRefreshToken);
    }

    public async Task RevokeTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = _jwtTokenService.HashToken(rawRefreshToken);
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

        if (refreshToken != null && refreshToken.IsActive)
        {
            refreshToken.RevokedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserDto> BootstrapInitialOwnerAsync(BootstrapOwnerRequest request, CancellationToken cancellationToken = default)
    {
        // Safe bootstrap guard: only allowed if system has 0 registered users
        var hasUsers = await _context.Users.AnyAsync(cancellationToken);
        if (hasUsers)
        {
            throw new InvalidOperationException("System is already bootstrapped. Additional user accounts must be created through authorized administration.");
        }

        // Ensure Owner role exists
        var ownerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner / Super Admin", cancellationToken);
        if (ownerRole == null)
        {
            ownerRole = new Role
            {
                Name = "Owner / Super Admin",
                Description = "Full business-wide administrative oversight and configuration",
                IsSystemRole = true
            };
            _context.Roles.Add(ownerRole);
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true,
            IsGlobalAdmin = true
        };

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = ownerRole.Id,
            AssignedAt = DateTimeOffset.UtcNow
        });

        _context.Users.Add(user);

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "BOOTSTRAP_SYSTEM_OWNER",
            UserId = user.Id,
            Username = user.Username,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            new List<string> { ownerRole.Name },
            new List<string>(),
            new List<Guid>(),
            null,
            true,
            true,
            user.LastLoginAt,
            user.CreatedAt
        );
    }

    public async Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            return null;
        }

        var user = await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return null;
        }

        var roles = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Name)
            .Distinct()
            .ToList();

        var permissions = user.UserRoles
            .Where(ur => ur.Role != null)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.Name)
            .Distinct()
            .ToList();

        var branchIds = user.UserBranches.Select(ub => ub.BranchId).ToList();

        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            roles,
            permissions,
            branchIds,
            user.DefaultBranchId,
            user.IsGlobalAdmin,
            user.IsActive,
            user.LastLoginAt,
            user.CreatedAt
        );
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;

namespace Saranga.Application.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetUsersAsync(UserFilterRequest filter, CancellationToken cancellationToken = default);
    Task<UserDto> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> ToggleUserStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto> AssignRolesAsync(Guid id, AssignUserRolesRequest request, CancellationToken cancellationToken = default);
    Task<UserDto> AssignBranchesAsync(Guid id, AssignUserBranchesRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, CancellationToken cancellationToken = default);
}

public class UserService : IUserService
{
    private readonly IAppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;

    public UserService(
        IAppDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(UserFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserBranches)
            .AsQueryable();

        // Branch filter or restriction for non-global admin
        if (!_currentUserService.IsGlobalAdmin && _currentUserService.AccessibleBranchIds.Count > 0)
        {
            var accessible = _currentUserService.AccessibleBranchIds;
            query = query.Where(u => u.UserBranches.Any(ub => accessible.Contains(ub.BranchId)) ||
                                     (u.DefaultBranchId.HasValue && accessible.Contains(u.DefaultBranchId.Value)));
        }

        if (filter.BranchId.HasValue && filter.BranchId != Guid.Empty)
        {
            query = query.Where(u => u.UserBranches.Any(ub => ub.BranchId == filter.BranchId.Value) ||
                                     u.DefaultBranchId == filter.BranchId.Value);
        }

        if (filter.RoleId.HasValue && filter.RoleId != Guid.Empty)
        {
            query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == filter.RoleId.Value));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(search) ||
                                     u.Email.ToLower().Contains(search) ||
                                     u.FullName.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : (filter.PageSize > 100 ? 100 : filter.PageSize);

        var items = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(
                u.Id,
                u.Username,
                u.Email,
                u.FullName,
                u.UserRoles.Where(ur => ur.Role != null).Select(ur => ur.Role!.Name).Distinct().ToList(),
                u.UserRoles.Where(ur => ur.Role != null).SelectMany(ur => ur.Role!.RolePermissions).Where(rp => rp.Permission != null).Select(rp => rp.Permission!.Name).Distinct().ToList(),
                u.UserBranches.Select(ub => ub.BranchId).ToList(),
                u.DefaultBranchId,
                u.IsGlobalAdmin,
                u.IsActive,
                u.LastLoginAt,
                u.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>(items, totalCount, page, pageSize);
    }

    public async Task<UserDto> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        return MapToDto(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedUsername = request.Username.Trim();
        var trimmedEmail = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new BusinessRuleException("Password must be at least 8 characters long.");
        }

        var usernameExists = await _context.Users.AnyAsync(u => u.Username.ToLower() == trimmedUsername.ToLower(), cancellationToken);
        if (usernameExists)
        {
            throw new BusinessRuleException($"Username '{request.Username}' is already taken.");
        }

        var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == trimmedEmail, cancellationToken);
        if (emailExists)
        {
            throw new BusinessRuleException($"Email '{request.Email}' is already registered.");
        }

        // Only Global Admin can grant Global Admin status
        var isGlobalAdmin = request.IsGlobalAdmin && _currentUserService.IsGlobalAdmin;

        var user = new User
        {
            Username = trimmedUsername,
            Email = trimmedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true,
            IsGlobalAdmin = isGlobalAdmin,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        // Assign branches
        if (request.BranchIds != null && request.BranchIds.Count > 0)
        {
            var validBranches = await _context.Branches
                .Where(b => request.BranchIds.Contains(b.Id) && b.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var b in validBranches)
            {
                user.UserBranches.Add(new UserBranch
                {
                    UserId = user.Id,
                    BranchId = b.Id,
                    IsDefault = request.DefaultBranchId.HasValue && request.DefaultBranchId.Value == b.Id,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }

            if (request.DefaultBranchId.HasValue && validBranches.Any(b => b.Id == request.DefaultBranchId.Value))
            {
                user.DefaultBranchId = request.DefaultBranchId.Value;
            }
            else if (validBranches.Count > 0)
            {
                user.DefaultBranchId = validBranches[0].Id;
                var firstUb = user.UserBranches.FirstOrDefault(ub => ub.BranchId == validBranches[0].Id);
                if (firstUb != null) firstUb.IsDefault = true;
            }
        }

        // Assign roles
        if (request.RoleIds != null && request.RoleIds.Count > 0)
        {
            var validRoles = await _context.Roles
                .Where(r => request.RoleIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

            foreach (var r in validRoles)
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = r.Id,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }
        }

        _context.Users.Add(user);

        // Audit Log (Strictly no sensitive password or hash data)
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "CREATE",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = user.DefaultBranchId,
            NewValues = JsonSerializer.Serialize(new
            {
                user.Username,
                user.Email,
                user.FullName,
                user.IsGlobalAdmin,
                user.IsActive,
                user.DefaultBranchId,
                RoleCount = user.UserRoles.Count,
                BranchCount = user.UserBranches.Count
            }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetUserByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        var trimmedEmail = request.Email.Trim().ToLowerInvariant();
        var emailExists = await _context.Users
            .AnyAsync(u => u.Id != id && u.Email.ToLower() == trimmedEmail, cancellationToken);

        if (emailExists)
        {
            throw new BusinessRuleException($"Email '{request.Email}' is already registered to another user.");
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            user.Email,
            user.FullName,
            user.DefaultBranchId
        });

        user.Email = trimmedEmail;
        user.FullName = request.FullName.Trim();

        if (request.DefaultBranchId.HasValue)
        {
            var hasBranch = user.UserBranches.Any(ub => ub.BranchId == request.DefaultBranchId.Value) || user.IsGlobalAdmin;
            if (!hasBranch)
            {
                throw new BusinessRuleException("Default branch must be one of the user's assigned branches.");
            }
            user.DefaultBranchId = request.DefaultBranchId.Value;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUserService.UserId;

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "UPDATE",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = user.DefaultBranchId,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new
            {
                user.Email,
                user.FullName,
                user.DefaultBranchId
            }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetUserByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> ToggleUserStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_currentUserService.UserId.HasValue && _currentUserService.UserId.Value == id)
        {
            throw new BusinessRuleException("You cannot deactivate your own administrative account.");
        }

        var user = await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        var previousStatus = user.IsActive;
        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUserService.UserId;

        // If user is being deactivated, IMMEDIATELY revoke all active refresh tokens!
        if (!user.IsActive)
        {
            foreach (var rt in user.RefreshTokens.Where(t => t.IsActive))
            {
                rt.RevokedAt = DateTimeOffset.UtcNow;
            }
        }

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = user.IsActive ? "USER_ACTIVATED" : "USER_DEACTIVATED",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = user.DefaultBranchId,
            OldValues = JsonSerializer.Serialize(new { IsActive = previousStatus }),
            NewValues = JsonSerializer.Serialize(new { user.IsActive }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetUserByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> AssignRolesAsync(Guid id, AssignUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        var validRoles = await _context.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        user.UserRoles.Clear();
        foreach (var r in validRoles)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = r.Id,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        // If user is assigned Owner / Super Admin role, ensure IsGlobalAdmin is true
        if (validRoles.Any(r => r.Name == SystemRoles.OwnerSuperAdmin))
        {
            user.IsGlobalAdmin = true;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUserService.UserId;

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "ROLES_ASSIGNED",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = user.DefaultBranchId,
            NewValues = JsonSerializer.Serialize(new
            {
                AssignedRoleCount = validRoles.Count,
                RoleNames = validRoles.Select(r => r.Name).ToList()
            }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetUserByIdAsync(user.Id, cancellationToken);
    }

    public async Task<UserDto> AssignBranchesAsync(Guid id, AssignUserBranchesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        var validBranches = await _context.Branches
            .Where(b => request.BranchIds.Contains(b.Id) && b.IsActive)
            .ToListAsync(cancellationToken);

        user.UserBranches.Clear();
        foreach (var b in validBranches)
        {
            user.UserBranches.Add(new UserBranch
            {
                UserId = user.Id,
                BranchId = b.Id,
                IsDefault = request.DefaultBranchId.HasValue && request.DefaultBranchId.Value == b.Id,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }

        if (request.DefaultBranchId.HasValue && validBranches.Any(b => b.Id == request.DefaultBranchId.Value))
        {
            user.DefaultBranchId = request.DefaultBranchId.Value;
        }
        else if (validBranches.Count > 0)
        {
            user.DefaultBranchId = validBranches[0].Id;
            var firstUb = user.UserBranches.FirstOrDefault(ub => ub.BranchId == validBranches[0].Id);
            if (firstUb != null) firstUb.IsDefault = true;
        }
        else if (!user.IsGlobalAdmin)
        {
            user.DefaultBranchId = null;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUserService.UserId;

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "BRANCHES_ASSIGNED",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = user.DefaultBranchId,
            NewValues = JsonSerializer.Serialize(new
            {
                AssignedBranchCount = validBranches.Count,
                BranchCodes = validBranches.Select(b => b.Code).ToList(),
                user.DefaultBranchId
            }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetUserByIdAsync(user.Id, cancellationToken);
    }

    public async Task ResetPasswordAsync(Guid id, ResetUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            throw new BusinessRuleException("New password must be at least 8 characters long.");
        }

        var user = await _context.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException("User", id);
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUserService.UserId;

        // Revoke all existing refresh tokens to force re-login on all devices
        foreach (var rt in user.RefreshTokens.Where(t => t.IsActive))
        {
            rt.RevokedAt = DateTimeOffset.UtcNow;
        }

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Action = "PASSWORD_RESET",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = user.DefaultBranchId,
            NewValues = JsonSerializer.Serialize(new { Message = "User password was reset by administrator." }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static UserDto MapToDto(User user)
    {
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

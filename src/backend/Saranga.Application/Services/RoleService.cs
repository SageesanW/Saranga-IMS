using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Roles;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;

namespace Saranga.Application.Services;

public interface IRoleService
{
    Task<List<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleDto> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleDto> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleDto> AssignPermissionsAsync(Guid id, AssignRolePermissionsRequest request, CancellationToken cancellationToken = default);
}

public class RoleService : IRoleService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RoleService(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderByDescending(r => r.IsSystemRole)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new RoleDto(
            r.Id,
            r.Name,
            r.Description,
            r.IsSystemRole,
            r.RolePermissions.Where(rp => rp.Permission != null).Select(rp => rp.Permission!.Name).OrderBy(p => p).ToList(),
            r.CreatedAt
        )).ToList();
    }

    public async Task<RoleDto> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role == null)
        {
            throw new NotFoundException("Role", id);
        }

        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.RolePermissions.Where(rp => rp.Permission != null).Select(rp => rp.Permission!.Name).OrderBy(p => p).ToList(),
            role.CreatedAt
        );
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name.Trim();

        var exists = await _context.Roles
            .AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException($"A role named '{request.Name}' already exists.");
        }

        var role = new Role
        {
            Name = trimmedName,
            Description = request.Description.Trim(),
            IsSystemRole = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.Roles.Add(role);

        // Assign permissions
        if (request.PermissionIds != null && request.PermissionIds.Count > 0)
        {
            var validPermissions = await _context.Permissions
                .Where(p => request.PermissionIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            foreach (var p in validPermissions)
            {
                role.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = p.Id,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }
        }

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Role),
            EntityId = role.Id.ToString(),
            Action = "CREATE",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = _currentUserService.BranchId,
            NewValues = JsonSerializer.Serialize(new
            {
                role.Name,
                role.Description,
                role.IsSystemRole,
                PermissionCount = role.RolePermissions.Count
            }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRoleByIdAsync(role.Id, cancellationToken);
    }

    public async Task<RoleDto> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role == null)
        {
            throw new NotFoundException("Role", id);
        }

        role.Description = request.Description.Trim();
        role.UpdatedAt = DateTimeOffset.UtcNow;
        role.UpdatedBy = _currentUserService.UserId;

        // If not Owner / Super Admin, update permissions
        if (!string.Equals(role.Name, SystemRoles.OwnerSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            role.RolePermissions.Clear();

            if (request.PermissionIds != null && request.PermissionIds.Count > 0)
            {
                var validPermissions = await _context.Permissions
                    .Where(p => request.PermissionIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);

                foreach (var p in validPermissions)
                {
                    role.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = p.Id,
                        AssignedAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Role),
            EntityId = role.Id.ToString(),
            Action = "UPDATE",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = _currentUserService.BranchId,
            NewValues = JsonSerializer.Serialize(new
            {
                role.Name,
                role.Description,
                PermissionCount = role.RolePermissions.Count
            }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRoleByIdAsync(role.Id, cancellationToken);
    }

    public async Task<RoleDto> AssignPermissionsAsync(Guid id, AssignRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role == null)
        {
            throw new NotFoundException("Role", id);
        }

        if (string.Equals(role.Name, SystemRoles.OwnerSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("The Owner / Super Admin role automatically possesses all permissions and cannot be modified.");
        }

        var oldPermCount = role.RolePermissions.Count;
        role.RolePermissions.Clear();

        if (request.PermissionIds != null && request.PermissionIds.Count > 0)
        {
            var validPermissions = await _context.Permissions
                .Where(p => request.PermissionIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            foreach (var p in validPermissions)
            {
                role.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = p.Id,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }
        }

        role.UpdatedAt = DateTimeOffset.UtcNow;
        role.UpdatedBy = _currentUserService.UserId;

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Role),
            EntityId = role.Id.ToString(),
            Action = "PERMISSIONS_ASSIGNED",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = _currentUserService.BranchId,
            OldValues = JsonSerializer.Serialize(new { PermissionCount = oldPermCount }),
            NewValues = JsonSerializer.Serialize(new { PermissionCount = role.RolePermissions.Count }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRoleByIdAsync(role.Id, cancellationToken);
    }
}

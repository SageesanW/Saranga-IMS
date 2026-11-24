using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Roles;

namespace Saranga.Application.Services;

public interface IPermissionService
{
    Task<List<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);
}

public class PermissionService : IPermissionService
{
    private readonly IAppDbContext _context;

    public PermissionService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .Select(p => new PermissionDto(
                p.Id,
                p.Name,
                p.Description,
                p.Module
            ))
            .ToListAsync(cancellationToken);

        return permissions;
    }
}

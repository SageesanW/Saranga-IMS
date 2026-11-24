using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;

namespace Saranga.Infrastructure.Services;

public class BranchAuthorizationService : IBranchAuthorizationService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public BranchAuthorizationService(
        IAppDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> CanAccessBranchAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return false;
        }

        // Global admin (Owner / Super Admin) has unrestricted business-wide oversight
        if (_currentUserService.IsGlobalAdmin)
        {
            if (!branchId.HasValue || branchId == Guid.Empty)
            {
                return true;
            }

            var branch = await _context.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == branchId.Value, cancellationToken);

            return branch != null && !branch.IsDeleted;
        }

        if (!branchId.HasValue || branchId == Guid.Empty)
        {
            return false;
        }

        // For regular users, branch must be active, not deleted, and explicitly assigned in UserBranches
        var isAssigned = await _context.UserBranches
            .AsNoTracking()
            .AnyAsync(ub => ub.UserId == _currentUserService.UserId.Value && ub.BranchId == branchId.Value, cancellationToken);

        if (!isAssigned)
        {
            return false;
        }

        var activeBranch = await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId.Value, cancellationToken);

        return activeBranch != null && !activeBranch.IsDeleted && activeBranch.IsActive;
    }

    public async Task EnsureBranchAccessAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        var hasAccess = await CanAccessBranchAsync(branchId, cancellationToken);
        if (!hasAccess)
        {
            throw new ForbiddenException($"User {_currentUserService.Username ?? "Unknown"} is not authorized to access branch '{branchId}'.");
        }
    }
}

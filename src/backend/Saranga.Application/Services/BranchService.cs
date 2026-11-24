using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Branches;
using Saranga.Domain.Entities;

namespace Saranga.Application.Services;

public interface IBranchService
{
    Task<List<BranchDto>> GetBranchesAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<BranchDto> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto> ToggleBranchStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

public class BranchService : IBranchService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public BranchService(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<BranchDto>> GetBranchesAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Branches.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }

        // If not global admin, filter by assigned branches unless they have general branch view
        if (!_currentUserService.IsGlobalAdmin && _currentUserService.AccessibleBranchIds.Count > 0)
        {
            var accessible = _currentUserService.AccessibleBranchIds;
            query = query.Where(b => accessible.Contains(b.Id));
        }

        var branches = await query
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(
                b.Id,
                b.Name,
                b.Code,
                b.Country,
                b.CurrencyCode,
                b.TimeZone,
                b.IsActive,
                b.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return branches;
    }

    public async Task<BranchDto> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (branch == null)
        {
            throw new NotFoundException("Branch", id);
        }

        return new BranchDto(
            branch.Id,
            branch.Name,
            branch.Code,
            branch.Country,
            branch.CurrencyCode,
            branch.TimeZone,
            branch.IsActive,
            branch.CreatedAt
        );
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var exists = await _context.Branches
            .AnyAsync(b => b.Code.ToUpper() == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException($"A branch with code '{request.Code}' already exists.");
        }

        var branch = new Branch
        {
            Name = request.Name.Trim(),
            Code = normalizedCode,
            Country = string.IsNullOrWhiteSpace(request.Country) ? "Sri Lanka" : request.Country.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "LKR" : request.CurrencyCode.Trim().ToUpperInvariant(),
            TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Colombo" : request.TimeZone.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.Branches.Add(branch);

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Branch),
            EntityId = branch.Id.ToString(),
            Action = "CREATE",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = branch.Id,
            NewValues = JsonSerializer.Serialize(new
            {
                branch.Name,
                branch.Code,
                branch.Country,
                branch.CurrencyCode,
                branch.TimeZone,
                branch.IsActive
            }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new BranchDto(
            branch.Id,
            branch.Name,
            branch.Code,
            branch.Country,
            branch.CurrencyCode,
            branch.TimeZone,
            branch.IsActive,
            branch.CreatedAt
        );
    }

    public async Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
        {
            throw new NotFoundException("Branch", id);
        }

        var oldValues = JsonSerializer.Serialize(new
        {
            branch.Name,
            branch.Country,
            branch.CurrencyCode,
            branch.TimeZone,
            branch.IsActive
        });

        branch.Name = request.Name.Trim();
        branch.Country = request.Country.Trim();
        branch.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        branch.TimeZone = request.TimeZone.Trim();
        branch.IsActive = request.IsActive;
        branch.UpdatedAt = DateTimeOffset.UtcNow;
        branch.UpdatedBy = _currentUserService.UserId;

        var newValues = JsonSerializer.Serialize(new
        {
            branch.Name,
            branch.Country,
            branch.CurrencyCode,
            branch.TimeZone,
            branch.IsActive
        });

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Branch),
            EntityId = branch.Id.ToString(),
            Action = "UPDATE",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = branch.Id,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new BranchDto(
            branch.Id,
            branch.Name,
            branch.Code,
            branch.Country,
            branch.CurrencyCode,
            branch.TimeZone,
            branch.IsActive,
            branch.CreatedAt
        );
    }

    public async Task<BranchDto> ToggleBranchStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
        {
            throw new NotFoundException("Branch", id);
        }

        var previousStatus = branch.IsActive;
        branch.IsActive = !branch.IsActive;
        branch.UpdatedAt = DateTimeOffset.UtcNow;
        branch.UpdatedBy = _currentUserService.UserId;

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Branch),
            EntityId = branch.Id.ToString(),
            Action = branch.IsActive ? "BRANCH_ACTIVATED" : "BRANCH_DEACTIVATED",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = branch.Id,
            OldValues = JsonSerializer.Serialize(new { IsActive = previousStatus }),
            NewValues = JsonSerializer.Serialize(new { branch.IsActive }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new BranchDto(
            branch.Id,
            branch.Name,
            branch.Code,
            branch.Country,
            branch.CurrencyCode,
            branch.TimeZone,
            branch.IsActive,
            branch.CreatedAt
        );
    }
}

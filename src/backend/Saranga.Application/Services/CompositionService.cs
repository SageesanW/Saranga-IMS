using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Compositions;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public interface ICompositionService
{
    Task<PagedResult<InventoryCompositionDto>> GetCompositionsAsync(CompositionQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<InventoryCompositionDto> GetCompositionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryCompositionDto> LookupByTagAsync(string tagNumber, CancellationToken cancellationToken = default);
    Task<InventoryCompositionDto> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<List<EligibleChildItemDto>> GetEligibleChildrenAsync(EligibleChildQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<InventoryCompositionDto> CreateCompositionAsync(CreateCompositionRequest request, CancellationToken cancellationToken = default);
    Task<InventoryCompositionDto> UpdateCompositionAsync(Guid id, UpdateCompositionRequest request, CancellationToken cancellationToken = default);
    Task<InventoryCompositionDto> AddMemberAsync(Guid compositionId, AddCompositionMemberRequest request, CancellationToken cancellationToken = default);
    Task<InventoryCompositionDto> RemoveMemberAsync(Guid compositionId, Guid inventoryItemId, CancellationToken cancellationToken = default);
    Task<InventoryCompositionDto> DissolveCompositionAsync(Guid compositionId, uint? rowVersion = null, CancellationToken cancellationToken = default);
    Task<MemberDetachmentResultDto?> DetachMemberOnSaleAsync(Guid inventoryItemId, string? saleInvoiceNumber = null, Guid? soldByUserId = null, CancellationToken cancellationToken = default);
}

public class CompositionService : ICompositionService
{
    private readonly IAppDbContext _context;
    private readonly IBranchAuthorizationService _branchAuthService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITagGeneratorService _tagGeneratorService;

    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public CompositionService(
        IAppDbContext context,
        IBranchAuthorizationService branchAuthService,
        ICurrentUserService currentUserService,
        ITagGeneratorService tagGeneratorService)
    {
        _context = context;
        _branchAuthService = branchAuthService;
        _currentUserService = currentUserService;
        _tagGeneratorService = tagGeneratorService;
    }

    public async Task<PagedResult<InventoryCompositionDto>> GetCompositionsAsync(CompositionQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryCompositions
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Category)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Material)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Purity)
            .AsNoTracking()
            .AsQueryable();

        // Branch Isolation / Filtering
        if (_currentUserService.IsGlobalAdmin)
        {
            if (queryParams.BranchId.HasValue && queryParams.BranchId != Guid.Empty)
            {
                query = query.Where(c => c.BranchId == queryParams.BranchId.Value);
            }
        }
        else
        {
            var targetBranchId = queryParams.BranchId ?? _currentUserService.BranchId;
            if (targetBranchId.HasValue && targetBranchId != Guid.Empty)
            {
                await _branchAuthService.EnsureBranchAccessAsync(targetBranchId.Value, cancellationToken);
                query = query.Where(c => c.BranchId == targetBranchId.Value);
            }
            else
            {
                var accessible = _currentUserService.AccessibleBranchIds;
                query = query.Where(c => accessible.Contains(c.BranchId));
            }
        }

        if (queryParams.StockLocationId.HasValue)
        {
            query = query.Where(c => c.StockLocationId == queryParams.StockLocationId.Value);
        }

        if (queryParams.Type.HasValue)
        {
            query = query.Where(c => (int)c.Type == queryParams.Type.Value);
        }

        if (queryParams.Status.HasValue)
        {
            query = query.Where(c => (int)c.Status == queryParams.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.TagNumber.ToLower().Contains(term) ||
                c.Barcode.ToLower().Contains(term) ||
                c.Description.ToLower().Contains(term)
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = queryParams.Page <= 0 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize <= 0 ? 20 : queryParams.PageSize;

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<InventoryCompositionDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<InventoryCompositionDto> GetCompositionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var composition = await _context.InventoryCompositions
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Category)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Material)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Purity)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (composition == null)
        {
            throw new NotFoundException("InventoryComposition", id);
        }

        await _branchAuthService.EnsureBranchAccessAsync(composition.BranchId, cancellationToken);
        return MapToDto(composition);
    }

    public async Task<InventoryCompositionDto> LookupByTagAsync(string tagNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tagNumber))
        {
            throw new BusinessRuleException("Tag number cannot be empty.");
        }

        var normalized = tagNumber.Trim().ToUpperInvariant();
        var composition = await _context.InventoryCompositions
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Category)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Material)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Purity)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TagNumber.ToUpper() == normalized, cancellationToken);

        if (composition == null)
        {
            throw new NotFoundException($"Composition with tag '{tagNumber}' was not found.");
        }

        await _branchAuthService.EnsureBranchAccessAsync(composition.BranchId, cancellationToken);
        return MapToDto(composition);
    }

    public async Task<InventoryCompositionDto> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            throw new BusinessRuleException("Barcode cannot be empty.");
        }

        var normalized = barcode.Trim().ToUpperInvariant();
        var composition = await _context.InventoryCompositions
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Category)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Material)
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i!.Purity)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Barcode.ToUpper() == normalized, cancellationToken);

        if (composition == null)
        {
            throw new NotFoundException($"Composition with barcode '{barcode}' was not found.");
        }

        await _branchAuthService.EnsureBranchAccessAsync(composition.BranchId, cancellationToken);
        return MapToDto(composition);
    }

    public async Task<List<EligibleChildItemDto>> GetEligibleChildrenAsync(EligibleChildQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(queryParams.BranchId, cancellationToken);

        var query = _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.Material)
            .Include(i => i.Purity)
            .Where(i => i.BranchId == queryParams.BranchId
                     && i.StockLocationId == queryParams.StockLocationId
                     && i.IsActive
                     && i.Status == InventoryStatus.Available)
            .AsQueryable();

        // Must not be currently in any active composition
        query = query.Where(i => !_context.InventoryCompositionMembers.Any(m => m.InventoryItemId == i.Id && m.IsActive));

        if (queryParams.CategoryId.HasValue)
        {
            query = query.Where(i => i.CategoryId == queryParams.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.TagNumber.ToLower().Contains(term) ||
                c.Barcode.ToLower().Contains(term) ||
                c.ItemCode.ToLower().Contains(term)
            );
        }

        var items = await query
            .OrderBy(i => i.Name)
            .Take(100)
            .ToListAsync(cancellationToken);

        return items.Select(i => new EligibleChildItemDto(
            i.Id,
            i.TagNumber,
            i.Barcode,
            i.ItemCode,
            i.Name,
            i.CategoryId,
            i.Category?.Name ?? string.Empty,
            i.Category?.Code ?? string.Empty,
            i.Material?.Name ?? string.Empty,
            i.Purity?.KaratValue ?? 0,
            i.GrossWeight,
            i.StoneWeight,
            i.NetWeight,
            i.PieceCount
        )).ToList();
    }

    public async Task<InventoryCompositionDto> CreateCompositionAsync(CreateCompositionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Composition name cannot be empty.");
        }

        if (!Enum.IsDefined(typeof(CompositionType), request.Type))
        {
            throw new BusinessRuleException("Invalid composition type. Must be Assembly (1) or Group (2).");
        }

        var compositionType = (CompositionType)request.Type;

        if (request.ChildItemIds == null)
        {
            throw new BusinessRuleException("Child item list cannot be null.");
        }

        if (request.ChildItemIds.Count != request.ChildItemIds.Distinct().Count())
        {
            throw new BusinessRuleException("Duplicate child items are not allowed in a composition request.");
        }

        var distinctChildIds = request.ChildItemIds.Distinct().ToList();
        if (distinctChildIds.Count < 2)
        {
            throw new BusinessRuleException("An Assembly or Group must contain at least 2 distinct existing inventory items.");
        }

        // 1. Branch Authorization & Verification
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        if (branch == null || !branch.IsActive)
        {
            throw new BusinessRuleException("Target branch does not exist or is inactive.");
        }

        // 2. Stock Location Validation
        var location = await _context.StockLocations.FirstOrDefaultAsync(l => l.Id == request.StockLocationId, cancellationToken);
        if (location == null)
        {
            throw new BusinessRuleException("Specified stock location does not exist.");
        }
        if (location.BranchId != request.BranchId)
        {
            throw new BusinessRuleException($"Stock location '{location.Code}' belongs to a different branch and cannot be assigned to a composition in '{branch.Code}'.");
        }
        if (!location.IsActive)
        {
            throw new BusinessRuleException($"Stock location '{location.Name}' is inactive.");
        }

        // 3. Automated Namespace-Separated Tag and Barcode Generation (No Manual Override Allowed)
        var (tagNumber, barcode) = await _tagGeneratorService.GenerateCompositionTagAndBarcodeAsync(branch.Code, compositionType, cancellationToken);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 4. Validate Child Items
            var children = await _context.InventoryItems
                .Include(i => i.Category)
                .Where(i => distinctChildIds.Contains(i.Id))
                .ToListAsync(cancellationToken);

            if (children.Count != distinctChildIds.Count)
            {
                throw new BusinessRuleException("One or more selected child items do not exist.");
            }

            // Verify each child's branch, location, status, and active state
            foreach (var child in children)
            {
                if (child.BranchId != request.BranchId)
                {
                    throw new BusinessRuleException($"Child item '{child.TagNumber}' belongs to a different branch and cannot be added.");
                }

                if (child.StockLocationId != request.StockLocationId)
                {
                    throw new BusinessRuleException($"Child item '{child.TagNumber}' is in stock location '{child.StockLocationId}', which does not match composition location '{request.StockLocationId}'. All children must be in the same physical location.");
                }

                if (!child.IsActive)
                {
                    throw new BusinessRuleException($"Child item '{child.TagNumber}' is inactive and cannot be added.");
                }

                if (child.Status == InventoryStatus.Sold)
                {
                    throw new BusinessRuleException($"Child item '{child.TagNumber}' is sold and cannot be added to a composition.");
                }

                if (child.Status == InventoryStatus.Reserved)
                {
                    throw new BusinessRuleException($"Child item '{child.TagNumber}' is reserved and cannot be added to a composition.");
                }

                if (child.Status != InventoryStatus.Available)
                {
                    throw new BusinessRuleException($"Child item '{child.TagNumber}' is not in Available status ({child.Status}).");
                }
            }

            // 5. Assembly Category Rule: All children must have the exact same Category
            if (compositionType == CompositionType.Assembly)
            {
                var distinctCategoryCount = children.Select(c => c.CategoryId).Distinct().Count();
                if (distinctCategoryCount > 1)
                {
                    throw new BusinessRuleException("All child items in an Assembly must belong to the exact same category. For items across multiple categories, create a Group instead.");
                }
            }

            // 6. Application check for active membership
            var alreadyActiveMembers = await _context.InventoryCompositionMembers
                .Where(m => distinctChildIds.Contains(m.InventoryItemId) && m.IsActive)
                .Select(m => m.InventoryItemId)
                .ToListAsync(cancellationToken);

            if (alreadyActiveMembers.Count > 0)
            {
                var conflictedChild = children.First(c => c.Id == alreadyActiveMembers[0]);
                throw new BusinessRuleException($"Child item '{conflictedChild.TagNumber}' is already part of another active composition.");
            }

            // 7. Create Composition Entity
            var composition = new InventoryComposition
            {
                BranchId = request.BranchId,
                StockLocationId = request.StockLocationId,
                Name = request.Name.Trim(),
                Type = compositionType,
                TagNumber = tagNumber,
                Barcode = barcode,
                Status = CompositionStatus.Active,
                Description = request.Description?.Trim() ?? string.Empty,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = _currentUserService.UserId
            };

            _context.InventoryCompositions.Add(composition);

            // 8. Add Members with explicit Added state to avoid EF Core navigation tracking anomalies
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var member = new InventoryCompositionMember
                {
                    CompositionId = composition.Id,
                    InventoryItemId = child.Id,
                    SortOrder = i + 1,
                    IsActive = true,
                    AddedAt = DateTimeOffset.UtcNow,
                    AddedBy = _currentUserService.UserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = _currentUserService.UserId
                };
                _context.InventoryCompositionMembers.Add(member);
            }

            // 9. Audit Logging
            RecordAudit("InventoryComposition", composition.Id, "COMPOSITION_CREATE", null, new
            {
                composition.BranchId,
                composition.StockLocationId,
                composition.Name,
                composition.Type,
                composition.TagNumber,
                composition.Barcode,
                ChildCount = children.Count,
                ChildIds = distinctChildIds
            }, composition.BranchId);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetCompositionByIdAsync(composition.Id, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("IX_InventoryCompositionMembers_InventoryItemId") || msg.Contains("23505"))
            {
                throw new BusinessRuleException("One or more selected child items are already part of another active composition. Please refresh available stock.");
            }
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<InventoryCompositionDto> UpdateCompositionAsync(Guid id, UpdateCompositionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Composition name cannot be empty.");
        }

        var composition = await _context.InventoryCompositions
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (composition == null)
        {
            throw new NotFoundException("InventoryComposition", id);
        }

        await _branchAuthService.EnsureBranchAccessAsync(composition.BranchId, cancellationToken);

        // Optimistic concurrency check: if client supplied RowVersion, enforce it against original loaded value
        if (request.RowVersion.HasValue && request.RowVersion.Value > 0)
        {
            _context.Entry(composition).Property(c => c.Version).OriginalValue = request.RowVersion.Value;
        }

        var oldValues = new { composition.Name, composition.Description };

        composition.Name = request.Name.Trim();
        composition.Description = request.Description?.Trim() ?? string.Empty;
        composition.UpdatedAt = DateTimeOffset.UtcNow;
        composition.UpdatedBy = _currentUserService.UserId;

        RecordAudit("InventoryComposition", composition.Id, "UPDATE", oldValues, new { composition.Name, composition.Description }, composition.BranchId);

        await _context.SaveChangesAsync(cancellationToken);
        return await GetCompositionByIdAsync(composition.Id, cancellationToken);
    }

    public async Task<InventoryCompositionDto> AddMemberAsync(Guid compositionId, AddCompositionMemberRequest request, CancellationToken cancellationToken = default)
    {
        var composition = await _context.InventoryCompositions
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
            .FirstOrDefaultAsync(c => c.Id == compositionId, cancellationToken);

        if (composition == null)
        {
            throw new NotFoundException("InventoryComposition", compositionId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(composition.BranchId, cancellationToken);

        if (composition.Status != CompositionStatus.Active)
        {
            throw new BusinessRuleException("Cannot add items to a dissolved composition.");
        }

        var child = await _context.InventoryItems
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == request.InventoryItemId, cancellationToken);

        if (child == null)
        {
            throw new NotFoundException("InventoryItem", request.InventoryItemId);
        }

        if (child.BranchId != composition.BranchId)
        {
            throw new BusinessRuleException("Child item belongs to a different branch than the composition.");
        }

        if (child.StockLocationId != composition.StockLocationId)
        {
            throw new BusinessRuleException("Child item is in a different stock location than the composition. Both must share the same physical location.");
        }

        if (!child.IsActive)
        {
            throw new BusinessRuleException($"Child item '{child.TagNumber}' is inactive.");
        }

        if (child.Status != InventoryStatus.Available)
        {
            throw new BusinessRuleException($"Child item '{child.TagNumber}' is in '{child.Status}' status and cannot be added.");
        }

        // Assembly Category constraint
        if (composition.Type == CompositionType.Assembly)
        {
            var activeMember = composition.Members.FirstOrDefault(m => m.IsActive && m.InventoryItem != null);
            if (activeMember != null && activeMember.InventoryItem!.CategoryId != child.CategoryId)
            {
                throw new BusinessRuleException($"All items in this Assembly must have category '{activeMember.InventoryItem.Category?.Name}'. Child item '{child.TagNumber}' has a different category.");
            }
        }

        // Check if child is already active in this or another composition
        var alreadyActive = await _context.InventoryCompositionMembers
            .AnyAsync(m => m.InventoryItemId == child.Id && m.IsActive, cancellationToken);

        if (alreadyActive)
        {
            throw new BusinessRuleException($"Child item '{child.TagNumber}' is already part of an active composition.");
        }

        var nextSort = request.SortOrder ?? composition.Members.Count;

        var member = new InventoryCompositionMember
        {
            CompositionId = composition.Id,
            InventoryItemId = child.Id,
            SortOrder = nextSort,
            IsActive = true,
            AddedAt = DateTimeOffset.UtcNow,
            AddedBy = _currentUserService.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.InventoryCompositionMembers.Add(member);

        // Touch parent composition update time to advance rowversion
        composition.UpdatedAt = DateTimeOffset.UtcNow;
        composition.UpdatedBy = _currentUserService.UserId;

        RecordAudit("InventoryComposition", composition.Id, "MEMBER_ADDED", null, new
        {
            MemberId = member.Id,
            child.TagNumber,
            child.Barcode,
            child.Name
        }, composition.BranchId);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("IX_InventoryCompositionMembers_InventoryItemId") || msg.Contains("23505"))
            {
                throw new BusinessRuleException($"Child item '{child.TagNumber}' is already part of another active composition.");
            }
            throw;
        }

        return await GetCompositionByIdAsync(composition.Id, cancellationToken);
    }

    public async Task<InventoryCompositionDto> RemoveMemberAsync(Guid compositionId, Guid inventoryItemId, CancellationToken cancellationToken = default)
    {
        var composition = await _context.InventoryCompositions
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
            .FirstOrDefaultAsync(c => c.Id == compositionId, cancellationToken);

        if (composition == null)
        {
            throw new NotFoundException("InventoryComposition", compositionId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(composition.BranchId, cancellationToken);

        if (composition.Status != CompositionStatus.Active)
        {
            throw new BusinessRuleException("Cannot remove items from a dissolved composition.");
        }

        var activeMembers = composition.Members.Where(m => m.IsActive).ToList();
        if (activeMembers.Count <= 2)
        {
            throw new BusinessRuleException("An active composition must have at least 2 members. Dissolve the composition instead of removing items below the minimum threshold.");
        }

        var member = activeMembers.FirstOrDefault(m => m.InventoryItemId == inventoryItemId);
        if (member == null)
        {
            throw new NotFoundException($"Active member with inventory item ID '{inventoryItemId}' not found in composition.");
        }

        // Close membership relationship without modifying child item record
        member.IsActive = false;
        member.RemovedAt = DateTimeOffset.UtcNow;
        member.RemovedBy = _currentUserService.UserId;
        member.UpdatedAt = DateTimeOffset.UtcNow;
        member.UpdatedBy = _currentUserService.UserId;

        // Touch parent composition update time to advance rowversion
        composition.UpdatedAt = DateTimeOffset.UtcNow;
        composition.UpdatedBy = _currentUserService.UserId;

        RecordAudit("InventoryComposition", composition.Id, "MEMBER_REMOVED", new
        {
            MemberId = member.Id,
            member.InventoryItemId,
            TagNumber = member.InventoryItem?.TagNumber
        }, null, composition.BranchId);

        await _context.SaveChangesAsync(cancellationToken);
        return await GetCompositionByIdAsync(composition.Id, cancellationToken);
    }

    public async Task<InventoryCompositionDto> DissolveCompositionAsync(Guid compositionId, uint? rowVersion = null, CancellationToken cancellationToken = default)
    {
        var composition = await _context.InventoryCompositions
            .Include(c => c.Members)
                .ThenInclude(m => m.InventoryItem)
            .FirstOrDefaultAsync(c => c.Id == compositionId, cancellationToken);

        if (composition == null)
        {
            throw new NotFoundException("InventoryComposition", compositionId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(composition.BranchId, cancellationToken);

        if (composition.Status == CompositionStatus.Dissolved)
        {
            throw new BusinessRuleException("Composition is already dissolved.");
        }

        // Optimistic concurrency check
        if (rowVersion.HasValue && rowVersion.Value > 0)
        {
            _context.Entry(composition).Property(c => c.Version).OriginalValue = rowVersion.Value;
        }

        var now = DateTimeOffset.UtcNow;
        var currentUserId = _currentUserService.UserId;

        composition.Status = CompositionStatus.Dissolved;
        composition.IsActive = false;
        composition.UpdatedAt = now;
        composition.UpdatedBy = currentUserId;

        // Close all active members so child items can be sold individually or composed again
        var activeMembers = composition.Members.Where(m => m.IsActive).ToList();
        foreach (var m in activeMembers)
        {
            m.IsActive = false;
            m.RemovedAt = now;
            m.RemovedBy = currentUserId;
            m.UpdatedAt = now;
            m.UpdatedBy = currentUserId;
        }

        RecordAudit("InventoryComposition", composition.Id, "COMPOSITION_DISSOLVED",
            new { ActiveMembersAtDissolution = activeMembers.Count },
            new { composition.Status, composition.IsActive },
            composition.BranchId);

        await _context.SaveChangesAsync(cancellationToken);
        return await GetCompositionByIdAsync(composition.Id, cancellationToken);
    }

    public async Task<MemberDetachmentResultDto?> DetachMemberOnSaleAsync(
        Guid inventoryItemId,
        string? saleInvoiceNumber = null,
        Guid? soldByUserId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Locate active membership
        var membership = await _context.InventoryCompositionMembers
            .Include(m => m.InventoryItem)
            .Include(m => m.Composition)
                .ThenInclude(c => c!.Members)
                    .ThenInclude(m => m.InventoryItem)
            .FirstOrDefaultAsync(m => m.InventoryItemId == inventoryItemId && m.IsActive, cancellationToken);

        if (membership == null || membership.Composition == null)
        {
            // Item was not part of an active composition, POS sale can proceed as single item
            return null;
        }

        var composition = membership.Composition;
        var userId = soldByUserId ?? _currentUserService.UserId;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var activeMembersBefore = composition.Members.Where(m => m.IsActive).ToList();
            int previousCount = activeMembersBefore.Count;

            // 2. Deactivate the detached member
            membership.IsActive = false;
            membership.RemovedAt = DateTimeOffset.UtcNow;
            membership.RemovedBy = userId;
            membership.UpdatedAt = DateTimeOffset.UtcNow;
            membership.UpdatedBy = userId;

            // Remaining active members excluding the detached item
            var remainingActive = activeMembersBefore.Where(m => m.Id != membership.Id).ToList();
            int remainingCount = remainingActive.Count;
            bool dissolved = false;

            // Touch parent composition update time to update concurrency token
            composition.UpdatedAt = DateTimeOffset.UtcNow;
            composition.UpdatedBy = userId;

            // 3. If fewer than 2 remain, automatically dissolve
            if (remainingCount < 2)
            {
                composition.Status = CompositionStatus.Dissolved;
                composition.IsActive = false;
                dissolved = true;

                // Deactivate any remaining solitary member so it is freed for independent operations
                foreach (var solitary in remainingActive)
                {
                    solitary.IsActive = false;
                    solitary.RemovedAt = DateTimeOffset.UtcNow;
                    solitary.RemovedBy = userId;
                    solitary.UpdatedAt = DateTimeOffset.UtcNow;
                    solitary.UpdatedBy = userId;
                }

                RecordAudit("InventoryComposition", composition.Id, "COMPOSITION_AUTO_DISSOLVED",
                    new { PreviousMemberCount = previousCount },
                    new { RemainingMemberCount = remainingCount, TriggeredByItem = membership.InventoryItem?.TagNumber, SaleInvoiceNumber = saleInvoiceNumber },
                    composition.BranchId);
            }

            RecordAudit("InventoryComposition", composition.Id, "MEMBER_DETACHED_SALE",
                new { MemberId = membership.Id, membership.InventoryItemId, membership.InventoryItem?.TagNumber },
                new { SaleInvoiceNumber = saleInvoiceNumber, RemainingMemberCount = remainingCount, CompositionStatus = composition.Status.ToString() },
                composition.BranchId);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // 4. Calculate updated totals for remaining active members
            decimal newGross = 0;
            decimal newStone = 0;
            int newPieces = 0;

            if (!dissolved)
            {
                foreach (var rm in remainingActive)
                {
                    if (rm.InventoryItem != null)
                    {
                        newGross += rm.InventoryItem.GrossWeight;
                        newStone += rm.InventoryItem.StoneWeight;
                        newPieces += rm.InventoryItem.PieceCount;
                    }
                }
            }

            return new MemberDetachmentResultDto(
                inventoryItemId,
                membership.InventoryItem?.TagNumber ?? string.Empty,
                composition.Id,
                composition.TagNumber,
                previousCount,
                dissolved ? 0 : remainingCount,
                dissolved,
                (int)composition.Status,
                composition.Status.ToString().ToUpperInvariant(),
                newGross,
                newStone,
                newGross - newStone,
                newPieces
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private void RecordAudit(string entityName, Guid entityId, string action, object? oldValues, object? newValues, Guid? branchId = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Action = action,
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = branchId ?? _currentUserService.BranchId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, AuditJsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, AuditJsonOptions) : null,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    private static InventoryCompositionDto MapToDto(InventoryComposition c)
    {
        var activeMembers = c.Members.Where(m => m.IsActive && m.InventoryItem != null).ToList();

        var totalGross = activeMembers.Sum(m => m.InventoryItem!.GrossWeight);
        var totalStone = activeMembers.Sum(m => m.InventoryItem!.StoneWeight);
        var totalNet = activeMembers.Sum(m => m.InventoryItem!.NetWeight);
        var totalPieces = activeMembers.Sum(m => m.InventoryItem!.PieceCount);

        var memberDtos = c.Members
            .OrderBy(m => m.SortOrder)
            .Select(m => new CompositionMemberDto(
                m.Id,
                m.InventoryItemId,
                m.InventoryItem?.Name ?? string.Empty,
                m.InventoryItem?.ItemCode ?? string.Empty,
                m.InventoryItem?.TagNumber ?? string.Empty,
                m.InventoryItem?.Barcode ?? string.Empty,
                m.InventoryItem?.CategoryId ?? Guid.Empty,
                m.InventoryItem?.Category?.Name ?? string.Empty,
                m.InventoryItem?.Category?.Code ?? string.Empty,
                m.InventoryItem?.Material?.Name ?? string.Empty,
                m.InventoryItem?.Purity?.KaratValue ?? 0,
                m.InventoryItem?.GrossWeight ?? 0,
                m.InventoryItem?.StoneWeight ?? 0,
                m.InventoryItem?.NetWeight ?? 0,
                m.InventoryItem?.PieceCount ?? 0,
                (int)(m.InventoryItem?.Status ?? InventoryStatus.Available),
                m.InventoryItem?.Status.ToString().ToUpperInvariant() ?? string.Empty,
                m.SortOrder,
                m.IsActive,
                m.AddedAt,
                m.RemovedAt
            )).ToList();

        return new InventoryCompositionDto(
            c.Id,
            c.BranchId,
            c.Branch?.Name ?? string.Empty,
            c.Branch?.Code ?? string.Empty,
            c.StockLocationId,
            c.StockLocation?.Name ?? string.Empty,
            c.StockLocation?.Code ?? string.Empty,
            c.Name,
            (int)c.Type,
            c.Type.ToString().ToUpperInvariant(),
            c.TagNumber,
            c.Barcode,
            (int)c.Status,
            c.Status.ToString().ToUpperInvariant(),
            c.Description,
            c.IsActive,
            totalGross,
            totalStone,
            totalNet,
            totalPieces,
            activeMembers.Count,
            c.Members.Count,
            memberDtos,
            c.Version,
            c.CreatedAt,
            c.CreatedBy,
            c.UpdatedAt
        );
    }
}

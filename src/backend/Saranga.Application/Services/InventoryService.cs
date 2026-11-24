using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Inventory;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public interface IInventoryService
{
    Task<PagedResult<InventoryItemDto>> GetInventoryAsync(InventoryQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> GetInventoryItemByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> CreateInventoryItemAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> ToggleInventoryItemActiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> ToggleInventoryItemStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> AdminStatusCorrectionAsync(Guid id, AdminStatusCorrectionRequest request, CancellationToken cancellationToken = default);
    Task<InventoryLookupDto> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<InventoryLookupDto> LookupByTagAsync(string tagNumber, CancellationToken cancellationToken = default);
}

public class InventoryService : IInventoryService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchAuthorizationService _branchAuthService;
    private readonly ITagGeneratorService _tagGeneratorService;

    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public InventoryService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IBranchAuthorizationService branchAuthService,
        ITagGeneratorService tagGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _branchAuthService = branchAuthService;
        _tagGeneratorService = tagGeneratorService;
    }

    public async Task<PagedResult<InventoryItemDto>> GetInventoryAsync(InventoryQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Branch)
            .Include(i => i.StockLocation)
            .Include(i => i.Material)
            .Include(i => i.Category)
            .Include(i => i.Purity)
            .Include(i => i.Specifications)
                .ThenInclude(s => s.Specification)
            .Include(i => i.Specifications)
                .ThenInclude(s => s.SpecificationOption)
            .Include(i => i.Media)
            .AsQueryable();

        // Branch Isolation: Global admin can view any branch; otherwise enforce accessible branches
        if (_currentUserService.IsGlobalAdmin)
        {
            if (queryParams.BranchId.HasValue && queryParams.BranchId != Guid.Empty)
            {
                query = query.Where(i => i.BranchId == queryParams.BranchId.Value);
            }
        }
        else
        {
            var targetBranchId = queryParams.BranchId ?? _currentUserService.BranchId;
            if (targetBranchId.HasValue && targetBranchId != Guid.Empty)
            {
                await _branchAuthService.EnsureBranchAccessAsync(targetBranchId.Value, cancellationToken);
                query = query.Where(i => i.BranchId == targetBranchId.Value);
            }
            else
            {
                var accessible = _currentUserService.AccessibleBranchIds;
                query = query.Where(i => accessible.Contains(i.BranchId));
            }
        }

        // Filters
        if (queryParams.StockLocationId.HasValue && queryParams.StockLocationId != Guid.Empty)
        {
            query = query.Where(i => i.StockLocationId == queryParams.StockLocationId.Value);
        }

        if (queryParams.MaterialId.HasValue && queryParams.MaterialId != Guid.Empty)
        {
            query = query.Where(i => i.MaterialId == queryParams.MaterialId.Value);
        }

        if (queryParams.CategoryId.HasValue && queryParams.CategoryId != Guid.Empty)
        {
            query = query.Where(i => i.CategoryId == queryParams.CategoryId.Value);
        }

        if (queryParams.PurityId.HasValue && queryParams.PurityId != Guid.Empty)
        {
            query = query.Where(i => i.PurityId == queryParams.PurityId.Value);
        }

        if (queryParams.Status.HasValue)
        {
            query = query.Where(i => (int)i.Status == queryParams.Status.Value);
        }

        if (queryParams.IsActive.HasValue)
        {
            query = query.Where(i => i.IsActive == queryParams.IsActive.Value);
        }

        // Search: Item Name, Item Code, Tag Number, Barcode
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var search = queryParams.Search.Trim().ToUpperInvariant();
            query = query.Where(i =>
                i.Name.ToUpper().Contains(search) ||
                i.ItemCode.ToUpper().Contains(search) ||
                i.TagNumber.ToUpper().Contains(search) ||
                i.Barcode.ToUpper().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = queryParams.Page > 0 ? queryParams.Page : 1;
        var pageSize = queryParams.PageSize > 0 ? queryParams.PageSize : 20;

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<InventoryItemDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<InventoryItemDto> GetInventoryItemByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Branch)
            .Include(i => i.StockLocation)
            .Include(i => i.Material)
            .Include(i => i.Category)
            .Include(i => i.Purity)
            .Include(i => i.Specifications)
                .ThenInclude(s => s.Specification)
            .Include(i => i.Specifications)
                .ThenInclude(s => s.SpecificationOption)
            .Include(i => i.Media)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (item == null)
        {
            throw new NotFoundException("InventoryItem", id);
        }

        await _branchAuthService.EnsureBranchAccessAsync(item.BranchId, cancellationToken);

        return MapToDto(item);
    }

    public async Task<InventoryItemDto> CreateInventoryItemAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Item name cannot be empty.");
        }

        // 1. Branch Authorization
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        if (branch == null || !branch.IsActive)
        {
            throw new BusinessRuleException("Target branch does not exist or is inactive.");
        }

        // 2. Stock Location Validation (Must belong to target branch and be active)
        var location = await _context.StockLocations.FirstOrDefaultAsync(l => l.Id == request.StockLocationId, cancellationToken);
        if (location == null)
        {
            throw new BusinessRuleException("Specified stock location does not exist.");
        }
        if (location.BranchId != request.BranchId)
        {
            throw new BusinessRuleException($"Stock location '{location.Code}' belongs to a different branch and cannot be assigned to an item in '{branch.Code}'.");
        }
        if (!location.IsActive)
        {
            throw new BusinessRuleException($"Stock location '{location.Name}' is inactive.");
        }

        // 3. Master References Validation (Must exist and be active)
        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken);
        if (material == null || !material.IsActive)
        {
            throw new BusinessRuleException("Selected material does not exist or is inactive.");
        }

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category == null || !category.IsActive)
        {
            throw new BusinessRuleException("Selected category does not exist or is inactive.");
        }

        var purity = await _context.Purities.FirstOrDefaultAsync(p => p.Id == request.PurityId, cancellationToken);
        if (purity == null || !purity.IsActive)
        {
            throw new BusinessRuleException("Selected purity does not exist or is inactive.");
        }

        // 4. Weight Rules
        if (request.GrossWeight < 0)
        {
            throw new BusinessRuleException("Gross weight cannot be negative.");
        }
        if (request.StoneWeight < 0)
        {
            throw new BusinessRuleException("Stone weight cannot be negative.");
        }
        if (request.StoneWeight > request.GrossWeight)
        {
            throw new BusinessRuleException($"Stone weight ({request.StoneWeight}g) cannot exceed gross weight ({request.GrossWeight}g).");
        }

        // Net weight calculated strictly by backend
        var netWeight = request.GrossWeight - request.StoneWeight;
        if (netWeight < 0)
        {
            throw new BusinessRuleException("Net weight cannot be negative.");
        }

        // 5. Piece Count
        if (request.PieceCount <= 0)
        {
            throw new BusinessRuleException("Piece count must be at least 1.");
        }

        // 6. Making Charge
        if (request.MakingChargeValue < 0)
        {
            throw new BusinessRuleException("Making charge value cannot be negative.");
        }
        if (!Enum.IsDefined(typeof(MakingChargeMethod), request.MakingChargeMethod))
        {
            throw new BusinessRuleException("Invalid making charge method.");
        }

        // 7. Tag and Barcode Generation / Validation
        string tagNumber;
        string barcode;

        if (string.IsNullOrWhiteSpace(request.TagNumber) || string.IsNullOrWhiteSpace(request.Barcode))
        {
            var generated = await _tagGeneratorService.GenerateTagAndBarcodeAsync(branch.Code, category.Code, cancellationToken);
            tagNumber = string.IsNullOrWhiteSpace(request.TagNumber) ? generated.TagNumber : request.TagNumber.Trim().ToUpperInvariant();
            barcode = string.IsNullOrWhiteSpace(request.Barcode) ? generated.Barcode : request.Barcode.Trim().ToUpperInvariant();
        }
        else
        {
            tagNumber = request.TagNumber.Trim().ToUpperInvariant();
            barcode = request.Barcode.Trim().ToUpperInvariant();
        }

        // Check Tag uniqueness
        var tagExists = await _context.InventoryItems.AnyAsync(i => i.TagNumber.ToUpper() == tagNumber, cancellationToken);
        if (tagExists)
        {
            throw new BusinessRuleException($"An inventory item with tag number '{tagNumber}' already exists.");
        }

        // Check Barcode uniqueness
        var barcodeExists = await _context.InventoryItems.AnyAsync(i => i.Barcode.ToUpper() == barcode, cancellationToken);
        if (barcodeExists)
        {
            throw new BusinessRuleException($"An inventory item with barcode '{barcode}' already exists.");
        }

        var itemCode = string.IsNullOrWhiteSpace(request.ItemCode)
            ? tagNumber
            : request.ItemCode.Trim().ToUpperInvariant();

        var item = new InventoryItem
        {
            BranchId = request.BranchId,
            StockLocationId = request.StockLocationId,
            Name = request.Name.Trim(),
            ItemCode = itemCode,
            TagNumber = tagNumber,
            Barcode = barcode,
            MaterialId = request.MaterialId,
            CategoryId = request.CategoryId,
            PurityId = request.PurityId,
            GrossWeight = request.GrossWeight,
            StoneWeight = request.StoneWeight,
            NetWeight = netWeight,
            PieceCount = request.PieceCount,
            MakingChargeMethod = (MakingChargeMethod)request.MakingChargeMethod,
            MakingChargeValue = request.MakingChargeValue,
            Status = InventoryStatus.Available,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        // 8. Specifications
        if (request.Specifications != null && request.Specifications.Count > 0)
        {
            foreach (var specReq in request.Specifications)
            {
                var itemSpec = await ValidateAndCreateItemSpecificationAsync(item.Id, specReq, cancellationToken);
                item.Specifications.Add(itemSpec);
            }
        }

        _context.InventoryItems.Add(item);

        RecordAudit("InventoryItem", item.Id, "CREATE", null, new
        {
            item.BranchId,
            item.StockLocationId,
            item.Name,
            item.ItemCode,
            item.TagNumber,
            item.Barcode,
            item.MaterialId,
            item.CategoryId,
            item.PurityId,
            item.GrossWeight,
            item.StoneWeight,
            item.NetWeight,
            item.PieceCount,
            item.MakingChargeMethod,
            item.MakingChargeValue,
            Status = item.Status.ToString()
        }, item.BranchId);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetInventoryItemByIdAsync(item.Id, cancellationToken);
    }

    public async Task<InventoryItemDto> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Item name cannot be empty.");
        }

        var item = await _context.InventoryItems
            .Include(i => i.Specifications)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (item == null)
        {
            throw new NotFoundException("InventoryItem", id);
        }

        // Branch Authorization
        await _branchAuthService.EnsureBranchAccessAsync(item.BranchId, cancellationToken);

        // Disallow arbitrary edits on sold items
        if (item.Status == InventoryStatus.Sold)
        {
            throw new BusinessRuleException("Sold inventory items are locked and cannot be edited.");
        }

        // 1. Stock Location Validation
        var location = await _context.StockLocations.FirstOrDefaultAsync(l => l.Id == request.StockLocationId, cancellationToken);
        if (location == null)
        {
            throw new BusinessRuleException("Specified stock location does not exist.");
        }
        if (location.BranchId != item.BranchId)
        {
            throw new BusinessRuleException("Stock location belongs to a different branch and cannot be assigned.");
        }
        if (!location.IsActive)
        {
            throw new BusinessRuleException($"Stock location '{location.Name}' is inactive.");
        }

        // 2. Master References Validation
        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken);
        if (material == null)
        {
            throw new BusinessRuleException("Selected material does not exist.");
        }

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category == null)
        {
            throw new BusinessRuleException("Selected category does not exist.");
        }

        var purity = await _context.Purities.FirstOrDefaultAsync(p => p.Id == request.PurityId, cancellationToken);
        if (purity == null)
        {
            throw new BusinessRuleException("Selected purity does not exist.");
        }

        // 3. Weight Rules
        if (request.GrossWeight < 0) throw new BusinessRuleException("Gross weight cannot be negative.");
        if (request.StoneWeight < 0) throw new BusinessRuleException("Stone weight cannot be negative.");
        if (request.StoneWeight > request.GrossWeight) throw new BusinessRuleException("Stone weight cannot exceed gross weight.");

        var netWeight = request.GrossWeight - request.StoneWeight;
        if (netWeight < 0) throw new BusinessRuleException("Net weight cannot be negative.");

        if (request.PieceCount <= 0) throw new BusinessRuleException("Piece count must be at least 1.");
        if (request.MakingChargeValue < 0) throw new BusinessRuleException("Making charge value cannot be negative.");
        if (!Enum.IsDefined(typeof(MakingChargeMethod), request.MakingChargeMethod)) throw new BusinessRuleException("Invalid making charge method.");

        var oldValues = new
        {
            item.Name,
            item.StockLocationId,
            item.GrossWeight,
            item.StoneWeight,
            item.NetWeight,
            item.PieceCount,
            item.MakingChargeMethod,
            item.MakingChargeValue,
            item.IsActive
        };

        item.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.ItemCode)) item.ItemCode = request.ItemCode.Trim().ToUpperInvariant();
        item.StockLocationId = request.StockLocationId;
        item.MaterialId = request.MaterialId;
        item.CategoryId = request.CategoryId;
        item.PurityId = request.PurityId;
        item.GrossWeight = request.GrossWeight;
        item.StoneWeight = request.StoneWeight;
        item.NetWeight = netWeight;
        item.PieceCount = request.PieceCount;
        item.MakingChargeMethod = (MakingChargeMethod)request.MakingChargeMethod;
        item.MakingChargeValue = request.MakingChargeValue;
        item.Description = request.Description?.Trim() ?? string.Empty;
        item.IsActive = request.IsActive;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = _currentUserService.UserId;

        // Update Specifications
        if (request.Specifications != null)
        {
            _context.InventoryItemSpecifications.RemoveRange(item.Specifications);
            item.Specifications.Clear();

            foreach (var specReq in request.Specifications)
            {
                var itemSpec = await ValidateAndCreateItemSpecificationAsync(item.Id, specReq, cancellationToken);
                item.Specifications.Add(itemSpec);
            }
        }

        RecordAudit("InventoryItem", item.Id, "UPDATE", oldValues, new
        {
            item.Name,
            item.StockLocationId,
            item.GrossWeight,
            item.StoneWeight,
            item.NetWeight,
            item.PieceCount,
            item.MakingChargeMethod,
            item.MakingChargeValue,
            item.IsActive
        }, item.BranchId);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetInventoryItemByIdAsync(item.Id, cancellationToken);
    }

    public async Task<InventoryItemDto> ToggleInventoryItemActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item == null)
        {
            throw new NotFoundException("InventoryItem", id);
        }

        await _branchAuthService.EnsureBranchAccessAsync(item.BranchId, cancellationToken);

        if (item.Status == InventoryStatus.Sold)
        {
            throw new BusinessRuleException("Sold items cannot be activated or deactivated.");
        }

        var prev = item.IsActive;
        item.IsActive = !item.IsActive;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = _currentUserService.UserId;

        RecordAudit("InventoryItem", item.Id, item.IsActive ? "ACTIVATED" : "DEACTIVATED", new { IsActive = prev }, new { item.IsActive }, item.BranchId);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetInventoryItemByIdAsync(item.Id, cancellationToken);
    }

    public Task<InventoryItemDto> ToggleInventoryItemStatusAsync(Guid id, CancellationToken cancellationToken = default)
        => ToggleInventoryItemActiveAsync(id, cancellationToken);

    public async Task<InventoryItemDto> AdminStatusCorrectionAsync(Guid id, AdminStatusCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
        {
            throw new BusinessRuleException("A mandatory audit reason (minimum 5 characters) is required for administrative status corrections.");
        }

        var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item == null)
        {
            throw new NotFoundException("InventoryItem", id);
        }

        await _branchAuthService.EnsureBranchAccessAsync(item.BranchId, cancellationToken);

        // Immutable SOLD guard
        if (item.Status == InventoryStatus.Sold)
        {
            throw new BusinessRuleException("Sold inventory items are locked and immutable. Reversals must be processed through authorized POS invoice reversal workflows.");
        }

        // Disallow setting SOLD directly via admin correction
        if (request.TargetStatus == (int)InventoryStatus.Sold)
        {
            throw new BusinessRuleException("Inventory items cannot be marked SOLD through administrative correction. Sold status is strictly controlled by POS and invoice finalization.");
        }

        if (!Enum.IsDefined(typeof(InventoryStatus), request.TargetStatus))
        {
            throw new BusinessRuleException("Invalid target inventory status.");
        }

        var targetStatusEnum = (InventoryStatus)request.TargetStatus;
        if (targetStatusEnum != InventoryStatus.Available && targetStatusEnum != InventoryStatus.Reserved)
        {
            throw new BusinessRuleException("Administrative status correction can only transition between Available and Reserved.");
        }

        if (item.Status == targetStatusEnum)
        {
            throw new BusinessRuleException($"Item is already in status '{targetStatusEnum}'.");
        }

        var prevStatus = item.Status;
        item.Status = targetStatusEnum;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = _currentUserService.UserId;

        RecordAudit("InventoryItem", item.Id, "ADMIN_STATUS_CORRECTION",
            new { Status = prevStatus.ToString() },
            new { Status = item.Status.ToString(), Reason = request.Reason.Trim() },
            item.BranchId);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetInventoryItemByIdAsync(item.Id, cancellationToken);
    }

    public async Task<InventoryLookupDto> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            throw new BusinessRuleException("Barcode cannot be empty.");
        }

        var normalized = barcode.Trim().ToUpperInvariant();
        var item = await _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Branch)
            .Include(i => i.StockLocation)
            .Include(i => i.Material)
            .Include(i => i.Category)
            .Include(i => i.Purity)
            .FirstOrDefaultAsync(i => i.Barcode.ToUpper() == normalized, cancellationToken);

        if (item == null)
        {
            throw new NotFoundException($"Inventory item with barcode '{barcode}' not found.");
        }

        await _branchAuthService.EnsureBranchAccessAsync(item.BranchId, cancellationToken);

        return MapToLookupDto(item);
    }

    public async Task<InventoryLookupDto> LookupByTagAsync(string tagNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tagNumber))
        {
            throw new BusinessRuleException("Tag number cannot be empty.");
        }

        var normalized = tagNumber.Trim().ToUpperInvariant();
        var item = await _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Branch)
            .Include(i => i.StockLocation)
            .Include(i => i.Material)
            .Include(i => i.Category)
            .Include(i => i.Purity)
            .FirstOrDefaultAsync(i => i.TagNumber.ToUpper() == normalized, cancellationToken);

        if (item == null)
        {
            throw new NotFoundException($"Inventory item with tag number '{tagNumber}' not found.");
        }

        await _branchAuthService.EnsureBranchAccessAsync(item.BranchId, cancellationToken);

        return MapToLookupDto(item);
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

    private static InventoryItemDto MapToDto(InventoryItem i)
    {
        return new InventoryItemDto(
            i.Id,
            i.BranchId,
            i.Branch?.Code ?? string.Empty,
            i.Branch?.Name ?? string.Empty,
            i.StockLocationId,
            i.StockLocation?.Code ?? string.Empty,
            i.StockLocation?.Name ?? string.Empty,
            i.StockLocation?.LocationType ?? string.Empty,
            i.Name,
            i.ItemCode,
            i.TagNumber,
            i.Barcode,
            i.MaterialId,
            i.Material?.Code ?? string.Empty,
            i.Material?.Name ?? string.Empty,
            i.CategoryId,
            i.Category?.Code ?? string.Empty,
            i.Category?.Name ?? string.Empty,
            i.PurityId,
            i.Purity?.Code ?? string.Empty,
            i.Purity?.Name ?? string.Empty,
            i.Purity?.KaratValue ?? 0,
            i.Purity?.PurityPercentage ?? 0,
            i.GrossWeight,
            i.StoneWeight,
            i.NetWeight,
            i.PieceCount,
            (int)i.MakingChargeMethod,
            i.MakingChargeMethod == MakingChargeMethod.PerGram ? "Per Gram" : "Per Item",
            i.MakingChargeValue,
            (int)i.Status,
            i.Status.ToString().ToUpperInvariant(),
            i.Description,
            i.IsActive,
            i.Specifications.Select(s => new InventoryItemSpecificationDto(
                s.Id,
                s.SpecificationId,
                s.Specification?.Code ?? string.Empty,
                s.Specification?.Name ?? string.Empty,
                s.SpecificationOptionId,
                s.SpecificationOption?.Code,
                s.SpecificationOption?.Value,
                s.CustomValue
            )).ToList(),
            i.Media.OrderBy(m => m.SortOrder).Select(m => new InventoryItemMediaDto(
                m.Id,
                m.FileName,
                m.StoragePath,
                m.ContentType,
                m.FileSize,
                (int)m.MediaType,
                m.MediaType.ToString().ToUpperInvariant(),
                m.SortOrder,
                m.IsPrimary,
                m.UploadedAt
            )).ToList(),
            i.CreatedAt,
            i.UpdatedAt
        );
    }

    private static InventoryLookupDto MapToLookupDto(InventoryItem i)
    {
        return new InventoryLookupDto(
            i.Id,
            i.BranchId,
            i.Branch?.Code ?? string.Empty,
            i.Branch?.Name ?? string.Empty,
            i.StockLocationId,
            i.StockLocation?.Code ?? string.Empty,
            i.StockLocation?.Name ?? string.Empty,
            i.TagNumber,
            i.Barcode,
            i.Name,
            i.Material?.Name ?? string.Empty,
            i.Category?.Name ?? string.Empty,
            i.Purity?.KaratValue ?? 0,
            i.GrossWeight,
            i.StoneWeight,
            i.NetWeight,
            i.PieceCount,
            (int)i.Status,
            i.Status.ToString().ToUpperInvariant(),
            (int)i.MakingChargeMethod,
            i.MakingChargeValue
        );
    }

    private async Task<InventoryItemSpecification> ValidateAndCreateItemSpecificationAsync(
        Guid inventoryItemId,
        CreateInventoryItemSpecificationRequest specReq,
        CancellationToken cancellationToken)
    {
        var spec = await _context.Specifications
            .Include(s => s.Options)
            .FirstOrDefaultAsync(s => s.Id == specReq.SpecificationId, cancellationToken);

        if (spec == null || !spec.IsActive)
        {
            throw new BusinessRuleException($"Specification with ID '{specReq.SpecificationId}' is invalid or inactive.");
        }

        var inputType = string.IsNullOrWhiteSpace(spec.InputType) ? SpecificationInputTypes.Predefined : spec.InputType.Trim().ToUpperInvariant();

        if (inputType == SpecificationInputTypes.Predefined)
        {
            if (!specReq.SpecificationOptionId.HasValue)
            {
                throw new BusinessRuleException($"Specification '{spec.Name}' requires a predefined option to be selected.");
            }
            if (!string.IsNullOrWhiteSpace(specReq.CustomValue))
            {
                throw new BusinessRuleException($"Specification '{spec.Name}' uses predefined options only; CustomValue is disallowed.");
            }
            var opt = spec.Options.FirstOrDefault(o => o.Id == specReq.SpecificationOptionId.Value);
            if (opt == null || !opt.IsActive)
            {
                throw new BusinessRuleException($"Specification option is invalid or inactive for specification '{spec.Name}'.");
            }
        }
        else if (inputType == SpecificationInputTypes.FreeText)
        {
            if (specReq.SpecificationOptionId.HasValue)
            {
                throw new BusinessRuleException($"Specification '{spec.Name}' is configured for free-text only; selecting a predefined option is disallowed.");
            }
            if (string.IsNullOrWhiteSpace(specReq.CustomValue))
            {
                throw new BusinessRuleException($"Specification '{spec.Name}' requires a custom value.");
            }
        }
        else if (inputType == SpecificationInputTypes.Hybrid)
        {
            if (!specReq.SpecificationOptionId.HasValue && string.IsNullOrWhiteSpace(specReq.CustomValue))
            {
                throw new BusinessRuleException($"Specification '{spec.Name}' requires either a predefined option or a custom value.");
            }
            if (specReq.SpecificationOptionId.HasValue)
            {
                var opt = spec.Options.FirstOrDefault(o => o.Id == specReq.SpecificationOptionId.Value);
                if (opt == null || !opt.IsActive)
                {
                    throw new BusinessRuleException($"Specification option is invalid or inactive for specification '{spec.Name}'.");
                }
            }
        }

        return new InventoryItemSpecification
        {
            InventoryItemId = inventoryItemId,
            SpecificationId = specReq.SpecificationId,
            SpecificationOptionId = specReq.SpecificationOptionId,
            CustomValue = specReq.CustomValue?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };
    }
}

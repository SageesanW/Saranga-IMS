using System.Security.Cryptography;
using System.Text;
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
    Task<InventoryItemDto> CreateInventoryItemAsync(CreateInventoryItemRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> ToggleInventoryItemActiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> ToggleInventoryItemStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryItemDto> AdminStatusCorrectionAsync(Guid id, AdminStatusCorrectionRequest request, CancellationToken cancellationToken = default);
    Task<InventoryLookupDto> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<InventoryLookupDto> LookupByTagAsync(string tagNumber, CancellationToken cancellationToken = default);
    Task<FileUploadResultDto> UploadFileAsync(Stream stream, string fileName, string contentType, long fileSize, bool isTemporary = true, CancellationToken cancellationToken = default);
    Task<InventoryItemMediaDto> AddMediaToItemAsync(Guid itemId, Stream stream, string fileName, string contentType, long fileSize, bool isPrimary = false, CancellationToken cancellationToken = default);
    Task RemoveMediaFromItemAsync(Guid itemId, Guid mediaId, CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredTempUploadsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
}

public class InventoryService : IInventoryService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchAuthorizationService _branchAuthService;
    private readonly ITagGeneratorService _tagGeneratorService;
    private readonly IInventoryValidationService _inventoryValidationService;

    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public InventoryService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IBranchAuthorizationService branchAuthService,
        ITagGeneratorService tagGeneratorService,
        IInventoryValidationService inventoryValidationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _branchAuthService = branchAuthService;
        _tagGeneratorService = tagGeneratorService;
        _inventoryValidationService = inventoryValidationService;
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

    public async Task<InventoryItemDto> CreateInventoryItemAsync(CreateInventoryItemRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Item name cannot be empty.");
        }

        // Module 5 Hardening: Idempotency Key Handling
        string? normalizedKey = idempotencyKey?.Trim();
        string requestHash = string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            var payloadJson = JsonSerializer.Serialize(request);
            requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));

            var existingRecord = await _context.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.UserId == _currentUserService.UserId && r.Key == normalizedKey, cancellationToken);

            if (existingRecord != null)
            {
                if (existingRecord.RequestHash != requestHash)
                {
                    throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
                }

                var cachedDto = JsonSerializer.Deserialize<InventoryItemDto>(existingRecord.ResponseBody);
                if (cachedDto != null)
                {
                    return cachedDto;
                }
            }
        }

        // 1. Branch Authorization
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        // 2. Comprehensive Domain Validation via Shared IInventoryValidationService
        var validated = await _inventoryValidationService.ValidateManualItemCreationAsync(request, cancellationToken);
        var branch = validated.Branch;
        var location = validated.StockLocation;
        var material = validated.Material;
        var category = validated.Category;
        var purity = validated.Purity;
        var netWeight = validated.NetWeight;

        // 3. Tag and Barcode Generation / Validation
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

        // 4. Specifications
        if (validated.Specifications != null && validated.Specifications.Count > 0)
        {
            foreach (var itemSpec in validated.Specifications)
            {
                itemSpec.InventoryItemId = item.Id;
                item.Specifications.Add(itemSpec);
            }
        }

        // 9. Media (Module 5 Hardening: promote temp uploads to permanent inventory uploads)
        if (request.Media != null && request.Media.Count > 0)
        {
            foreach (var m in request.Media)
            {
                var storagePath = m.StoragePath.Trim();
                if (storagePath.StartsWith("/uploads/temp/", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(storagePath);
                    var sourcePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp", fileName);
                    var destDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "inventory");
                    Directory.CreateDirectory(destDir);
                    var destPath = Path.Combine(destDir, fileName);
                    if (File.Exists(sourcePath))
                    {
                        File.Move(sourcePath, destPath, overwrite: true);
                    }
                    storagePath = $"/uploads/inventory/{fileName}";
                }

                item.Media.Add(new InventoryItemMedia
                {
                    InventoryItemId = item.Id,
                    FileName = m.FileName.Trim(),
                    StoragePath = storagePath,
                    ContentType = m.ContentType.Trim(),
                    FileSize = m.FileSize,
                    MediaType = (InventoryMediaType)m.MediaType,
                    SortOrder = m.SortOrder,
                    IsPrimary = m.IsPrimary,
                    UploadedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = _currentUserService.UserId
                });
            }
        }

        InventoryItemDto resultDto;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
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

            resultDto = await GetInventoryItemByIdAsync(item.Id, cancellationToken);

            if (!string.IsNullOrWhiteSpace(normalizedKey))
            {
                var record = new IdempotencyRecord
                {
                    Key = normalizedKey,
                    UserId = _currentUserService.UserId,
                    Endpoint = "POST /api/inventory",
                    RequestHash = requestHash,
                    ResponseStatusCode = 201,
                    ResponseBody = JsonSerializer.Serialize(resultDto),
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = _currentUserService.UserId
                };
                _context.IdempotencyRecords.Add(record);
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return resultDto;
    }

    public async Task<InventoryItemDto> UpdateInventoryItemAsync(Guid id, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Item name cannot be empty.");
        }

        var item = await _context.InventoryItems
            .Include(i => i.Specifications)
            .Include(i => i.Media)
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

        // Domain Validation via Shared IInventoryValidationService
        var validated = await _inventoryValidationService.ValidateManualItemUpdateAsync(request, item.BranchId, cancellationToken);
        var netWeight = validated.NetWeight;

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

        // Concurrency token verification
        if (request.Version.HasValue && request.Version.Value != 0)
        {
            _context.Entry(item).Property(i => i.Version).OriginalValue = request.Version.Value;
        }

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

        // Update Media
        if (request.Media != null)
        {
            _context.InventoryItemMedia.RemoveRange(item.Media);
            item.Media.Clear();

            foreach (var m in request.Media)
            {
                item.Media.Add(new InventoryItemMedia
                {
                    InventoryItemId = item.Id,
                    FileName = m.FileName.Trim(),
                    StoragePath = m.StoragePath.Trim(),
                    ContentType = m.ContentType.Trim(),
                    FileSize = m.FileSize,
                    MediaType = (InventoryMediaType)m.MediaType,
                    SortOrder = m.SortOrder,
                    IsPrimary = m.IsPrimary,
                    UploadedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = _currentUserService.UserId
                });
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
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
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

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
            i.Version,
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

        var specErr = _inventoryValidationService.ValidateSpecification(spec, specReq.SpecificationOptionId, specReq.CustomValue);
        if (specErr != null)
        {
            throw new BusinessRuleException(specErr);
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

    public async Task<FileUploadResultDto> UploadFileAsync(
        Stream stream,
        string fileName,
        string contentType,
        long fileSize,
        bool isTemporary = true,
        CancellationToken cancellationToken = default)
    {
        if (stream == null || fileSize <= 0)
        {
            throw new BusinessRuleException("Uploaded file cannot be empty.");
        }

        var normalizedContentType = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        var isImage = normalizedContentType is "image/jpeg" or "image/png" or "image/webp";
        var isDoc = normalizedContentType is "application/pdf";

        if (!isImage && !isDoc)
        {
            throw new BusinessRuleException("Unsupported file type. Only JPEG, PNG, WebP images and PDF documents are allowed.");
        }

        // 5MB limit for images, 10MB limit for documents
        var maxBytes = isImage ? 5 * 1024 * 1024 : 10 * 1024 * 1024;
        if (fileSize > maxBytes)
        {
            var maxMb = isImage ? 5 : 10;
            throw new BusinessRuleException($"File size exceeds the allowable limit of {maxMb}MB.");
        }

        var safeExtension = Path.GetExtension(fileName).ToLowerInvariant();
        var safeFileName = $"{Guid.NewGuid():N}{safeExtension}";
        var subFolder = isTemporary ? "temp" : "inventory";
        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads", subFolder);
        Directory.CreateDirectory(uploadFolder);
        var fullPath = Path.Combine(uploadFolder, safeFileName);

        await using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        var storageRelativePath = $"/uploads/{subFolder}/{safeFileName}";
        var mediaType = isImage ? (int)InventoryMediaType.Image : (int)InventoryMediaType.Document;

        return new FileUploadResultDto(
            Path.GetFileName(fileName),
            storageRelativePath,
            normalizedContentType,
            fileSize,
            mediaType
        );
    }

    public async Task<InventoryItemMediaDto> AddMediaToItemAsync(
        Guid itemId,
        Stream stream,
        string fileName,
        string contentType,
        long fileSize,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        var item = await _context.InventoryItems
            .Include(i => i.Media)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new NotFoundException("InventoryItem", itemId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(item.BranchId, cancellationToken);

        if (item.Status == InventoryStatus.Sold)
        {
            throw new BusinessRuleException("Sold inventory items are locked and cannot have media attached.");
        }

        var uploadResult = await UploadFileAsync(stream, fileName, contentType, fileSize, isTemporary: false, cancellationToken);

        if (isPrimary && item.Media.Count > 0)
        {
            foreach (var existing in item.Media)
            {
                existing.IsPrimary = false;
            }
        }

        var media = new InventoryItemMedia
        {
            InventoryItemId = item.Id,
            FileName = uploadResult.FileName,
            StoragePath = uploadResult.StoragePath,
            ContentType = uploadResult.ContentType,
            FileSize = uploadResult.FileSize,
            MediaType = (InventoryMediaType)uploadResult.MediaType,
            SortOrder = item.Media.Count,
            IsPrimary = isPrimary || item.Media.Count == 0,
            UploadedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.InventoryItemMedia.Add(media);

        RecordAudit("InventoryItem", item.Id, "UPDATE", null, new
        {
            Action = "AddMedia",
            MediaId = media.Id,
            uploadResult.FileName,
            uploadResult.StoragePath
        }, item.BranchId);

        await _context.SaveChangesAsync(cancellationToken);

        return new InventoryItemMediaDto(
            media.Id,
            media.FileName,
            media.StoragePath,
            media.ContentType,
            media.FileSize,
            (int)media.MediaType,
            media.MediaType.ToString().ToUpperInvariant(),
            media.SortOrder,
            media.IsPrimary,
            media.UploadedAt
        );
    }

    public async Task RemoveMediaFromItemAsync(Guid itemId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var item = await _context.InventoryItems
            .Include(i => i.Media)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

        if (item == null)
        {
            throw new NotFoundException("InventoryItem", itemId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(item.BranchId, cancellationToken);

        if (item.Status == InventoryStatus.Sold)
        {
            throw new BusinessRuleException("Sold inventory items are locked and cannot have media removed.");
        }

        var media = item.Media.FirstOrDefault(m => m.Id == mediaId);
        if (media == null)
        {
            throw new NotFoundException("InventoryItemMedia", mediaId);
        }

        _context.InventoryItemMedia.Remove(media);

        try
        {
            var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), media.StoragePath.TrimStart('/'));
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
        catch
        {
            // Ignore failure to delete non-critical local file
        }

        RecordAudit("InventoryItem", item.Id, "UPDATE", new
        {
            Action = "RemoveMedia",
            MediaId = media.Id,
            media.FileName
        }, null, item.BranchId);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CleanupExpiredTempUploadsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "temp");
        if (!Directory.Exists(tempFolder))
        {
            return Task.FromResult(0);
        }

        var threshold = DateTime.UtcNow - maxAge;
        var count = 0;

        foreach (var filePath in Directory.GetFiles(tempFolder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.LastWriteTimeUtc < threshold)
            {
                try
                {
                    fileInfo.Delete();
                    count++;
                }
                catch
                {
                    // Ignore locked files
                }
            }
        }

        return Task.FromResult(count);
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.InventoryCount;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public class InventoryCountService : IInventoryCountService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchAuthorizationService _branchAuthService;
    private readonly ILogger<InventoryCountService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public InventoryCountService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IBranchAuthorizationService branchAuthService,
        ILogger<InventoryCountService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _branchAuthService = branchAuthService;
        _logger = logger;
    }

    public async Task<PagedResult<InventoryCountDto>> GetCountsAsync(InventoryCountQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryCounts
            .AsNoTracking()
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .AsQueryable();

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
                return new PagedResult<InventoryCountDto>(new List<InventoryCountDto>(), 0, queryParams.Page, queryParams.PageSize);
            }
        }

        if (queryParams.StockLocationId.HasValue && queryParams.StockLocationId != Guid.Empty)
        {
            query = query.Where(c => c.StockLocationId == queryParams.StockLocationId.Value);
        }

        if (queryParams.Status.HasValue)
        {
            query = query.Where(c => c.Status == queryParams.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = queryParams.Page < 1 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize < 1 ? 20 : (queryParams.PageSize > 100 ? 100 : queryParams.PageSize);

        var items = await query
            .OrderByDescending(c => c.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = items.Select(c => c.StartedBy)
            .Concat(items.Where(c => c.ReconciledBy.HasValue).Select(c => c.ReconciledBy!.Value))
            .Concat(items.Where(c => c.CompletedBy.HasValue).Select(c => c.CompletedBy!.Value))
            .Distinct()
            .ToList();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var dtos = items.Select(c => MapToDto(c, users)).ToList();

        return new PagedResult<InventoryCountDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<InventoryCountDto> GetCountByIdAsync(Guid countId, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .AsNoTracking()
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        var userIds = new List<Guid> { count.StartedBy };
        if (count.ReconciledBy.HasValue) userIds.Add(count.ReconciledBy.Value);
        if (count.CompletedBy.HasValue) userIds.Add(count.CompletedBy.Value);

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return MapToDto(count, users);
    }

    public async Task<PagedResult<InventoryCountLineDto>> GetCountLinesAsync(Guid countId, bool? isCounted = null, CountLineResult? result = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        var query = _context.InventoryCountLines
            .AsNoTracking()
            .Where(l => l.InventoryCountId == countId);

        if (isCounted.HasValue)
        {
            query = query.Where(l => l.IsCounted == isCounted.Value);
        }

        if (result.HasValue)
        {
            query = query.Where(l => l.Result == result.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 50 : (pageSize > 200 ? 200 : pageSize);

        var lines = await query
            .OrderBy(l => l.TagNumberSnapshot)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = lines.Select(MapLineToDto).ToList();

        return new PagedResult<InventoryCountLineDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<List<InventoryCountUnexpectedItemDto>> GetUnexpectedItemsAsync(Guid countId, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        var items = await _context.InventoryCountUnexpectedItems
            .AsNoTracking()
            .Where(u => u.InventoryCountId == countId)
            .OrderByDescending(u => u.ScannedAt)
            .ToListAsync(cancellationToken);

        return items.Select(MapUnexpectedToDto).ToList();
    }

    public async Task<CountVariancesDto> GetCountVariancesAsync(Guid countId, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        var missingLines = await _context.InventoryCountLines
            .AsNoTracking()
            .Where(l => l.InventoryCountId == countId && l.Result == CountLineResult.Missing)
            .OrderBy(l => l.TagNumberSnapshot)
            .ToListAsync(cancellationToken);

        var unexpectedItems = await _context.InventoryCountUnexpectedItems
            .AsNoTracking()
            .Where(u => u.InventoryCountId == countId)
            .OrderByDescending(u => u.ScannedAt)
            .ToListAsync(cancellationToken);

        return new CountVariancesDto(
            countId,
            missingLines.Count,
            unexpectedItems.Count,
            missingLines.Select(MapLineToDto).ToList(),
            unexpectedItems.Select(MapUnexpectedToDto).ToList()
        );
    }

    public async Task<InventoryCountActiveCheckDto> CheckActiveCountForLocationAsync(Guid branchId, Guid stockLocationId, CancellationToken cancellationToken = default)
    {
        var activeCount = await _context.InventoryCounts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.BranchId == branchId
                                   && c.StockLocationId == stockLocationId
                                   && (c.Status == InventoryCountStatus.InProgress || c.Status == InventoryCountStatus.RequiresReview),
                                 cancellationToken);

        if (activeCount != null)
        {
            return new InventoryCountActiveCheckDto(
                true,
                activeCount.Id,
                (int)activeCount.Status,
                FormatStatusName(activeCount.Status),
                activeCount.StartedAt
            );
        }

        return new InventoryCountActiveCheckDto(false, null, null, null, null);
    }

    public async Task<InventoryCountDto> StartCountAsync(StartInventoryCountRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        if (branch == null || !branch.IsActive)
        {
            throw new BusinessRuleException("Selected branch does not exist or is inactive.");
        }

        var location = await _context.StockLocations.FirstOrDefaultAsync(l => l.Id == request.StockLocationId, cancellationToken);
        if (location == null)
        {
            throw new BusinessRuleException("Selected stock location does not exist.");
        }
        if (location.BranchId != request.BranchId)
        {
            throw new BusinessRuleException($"Stock location '{location.Code}' does not belong to branch '{branch.Code}'.");
        }
        if (!location.IsActive)
        {
            throw new BusinessRuleException($"Stock location '{location.Name}' is inactive.");
        }

        // Check if an active count already exists for this branch + location
        var activeCountExists = await _context.InventoryCounts.AnyAsync(
            c => c.BranchId == request.BranchId
              && c.StockLocationId == request.StockLocationId
              && (c.Status == InventoryCountStatus.InProgress || c.Status == InventoryCountStatus.RequiresReview),
            cancellationToken);

        if (activeCountExists)
        {
            throw new BusinessRuleException($"An active inventory count is already in progress for stock location '{location.Name}'. Only one active count per location is permitted.");
        }

        // Query expected physical InventoryItems (Available + Reserved, excluding Sold)
        // Invariant: Physical items in Assemblies/Groups are included as their individual InventoryItem units
        var expectedItems = await _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Material)
            .Include(i => i.Category)
            .Include(i => i.Purity)
            .Where(i => i.BranchId == request.BranchId
                     && i.StockLocationId == request.StockLocationId
                     && i.IsActive
                     && i.Status != InventoryStatus.Sold)
            .ToListAsync(cancellationToken);

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;

        var countSession = new InventoryCount
        {
            BranchId = request.BranchId,
            StockLocationId = request.StockLocationId,
            Status = InventoryCountStatus.InProgress,
            StartedAt = now,
            StartedBy = userId,
            ExpectedItemCount = expectedItems.Count,
            CountedExpectedItemCount = 0,
            MatchedCount = 0,
            MissingCount = 0,
            UnexpectedCount = 0,
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            CreatedBy = userId
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.InventoryCounts.Add(countSession);
            await _context.SaveChangesAsync(cancellationToken);

            // Create frozen baseline count lines
            if (expectedItems.Count > 0)
            {
                var lines = expectedItems.Select(item => new InventoryCountLine
                {
                    InventoryCountId = countSession.Id,
                    InventoryItemId = item.Id,
                    ExpectedBranchId = request.BranchId,
                    ExpectedStockLocationId = request.StockLocationId,
                    TagNumberSnapshot = item.TagNumber,
                    BarcodeSnapshot = item.Barcode,
                    ItemNameSnapshot = item.Name,
                    MaterialNameSnapshot = item.Material?.Name ?? string.Empty,
                    CategoryNameSnapshot = item.Category?.Name ?? string.Empty,
                    PurityNameSnapshot = item.Purity?.Name ?? string.Empty,
                    GrossWeightSnapshot = item.GrossWeight,
                    StoneWeightSnapshot = item.StoneWeight,
                    NetWeightSnapshot = item.NetWeight,
                    PieceCountSnapshot = item.PieceCount,
                    ExpectedStatusSnapshot = item.Status.ToString().ToUpperInvariant(),
                    IsExpected = true,
                    IsCounted = false,
                    Result = CountLineResult.Pending,
                    ResolutionStatus = VarianceResolutionStatus.Unresolved,
                    CreatedAt = now,
                    CreatedBy = userId
                }).ToList();

                _context.InventoryCountLines.AddRange(lines);
                await _context.SaveChangesAsync(cancellationToken);
            }

            RecordAudit("InventoryCount", countSession.Id, "COUNT_STARTED",
                null,
                new { countSession.BranchId, countSession.StockLocationId, countSession.ExpectedItemCount, Status = countSession.Status.ToString() },
                request.BranchId);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_InventoryCounts_BranchId_StockLocationId") == true)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new BusinessRuleException($"An active inventory count is already in progress for stock location '{location.Name}'. Duplicate active counts are prohibited.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await GetCountByIdAsync(countSession.Id, cancellationToken);
    }

    public async Task<ScanResultDto> ScanItemAsync(Guid countId, ScanInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        if (count.Status != InventoryCountStatus.InProgress)
        {
            throw new BusinessRuleException($"Cannot scan items: inventory count is in '{count.Status.ToString().ToUpperInvariant()}' state.");
        }

        var rawIdentifier = request.Identifier?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawIdentifier))
        {
            throw new BusinessRuleException("Barcode or Tag Number cannot be empty.");
        }

        var normalized = rawIdentifier.ToUpperInvariant();
        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;

        // 1. Look up InventoryItem by Tag or Barcode
        var item = await _context.InventoryItems
            .Include(i => i.Material)
            .Include(i => i.Category)
            .Include(i => i.Purity)
            .Include(i => i.StockLocation)
            .FirstOrDefaultAsync(i => i.TagNumber.ToUpper() == normalized || i.Barcode.ToUpper() == normalized, cancellationToken);

        // 2. If not found as single item, check if it's an Assembly or Group parent composition
        if (item == null)
        {
            var composition = await _context.InventoryCompositions
                .Include(c => c.Members.Where(m => m.IsActive))
                    .ThenInclude(m => m.InventoryItem)
                        .ThenInclude(i => i.Category)
                .Include(c => c.Members.Where(m => m.IsActive))
                    .ThenInclude(m => m.InventoryItem)
                        .ThenInclude(i => i.Purity)
                .FirstOrDefaultAsync(c => (c.TagNumber.ToUpper() == normalized || c.Barcode.ToUpper() == normalized) && c.IsActive, cancellationToken);

            if (composition != null)
            {
                // Invariant: Composition parent is NEVER counted as a stock unit!
                // Return preview with active child members for confirmation
                var activeMembers = composition.Members.Where(m => m.IsActive && m.InventoryItem != null).ToList();

                var memberItemIds = activeMembers.Select(m => m.InventoryItemId).ToList();
                var existingLines = await _context.InventoryCountLines
                    .AsNoTracking()
                    .Where(l => l.InventoryCountId == countId && memberItemIds.Contains(l.InventoryItemId))
                    .ToDictionaryAsync(l => l.InventoryItemId, l => l, cancellationToken);

                var memberDtos = activeMembers.Select(m =>
                {
                    var child = m.InventoryItem!;
                    var inBaseline = existingLines.TryGetValue(child.Id, out var line);
                    var isCounted = inBaseline && line!.IsCounted;
                    return new CompositionMemberScanPreviewDto(
                        child.Id,
                        child.TagNumber,
                        child.Barcode,
                        child.Name,
                        child.Category?.Name ?? string.Empty,
                        child.Purity?.KaratValue ?? 0m,
                        child.GrossWeight,
                        isCounted,
                        inBaseline
                    );
                }).ToList();

                var preview = new CompositionScanPreviewDto(
                    composition.Id,
                    composition.Name,
                    composition.TagNumber,
                    composition.Barcode,
                    composition.Type.ToString(),
                    memberDtos
                );

                var currentSummary = MapToDto(count, new Dictionary<Guid, string>());
                return new ScanResultDto(
                    "COMPOSITION_PARENT",
                    $"Composition '{composition.Name}' ({composition.TagNumber}) found with {activeMembers.Count} active physical members. Please verify physical presence of individual items.",
                    null,
                    null,
                    preview,
                    currentSummary
                );
            }

            // Not found anywhere
            var notFoundSummary = MapToDto(count, new Dictionary<Guid, string>());
            return new ScanResultDto(
                "NOT_FOUND",
                $"No jewellery item or composition was found matching '{rawIdentifier}'.",
                null,
                null,
                null,
                notFoundSummary
            );
        }

        // 3. Cross-Branch Item Verification
        if (item.BranchId != count.BranchId)
        {
            var summary = MapToDto(count, new Dictionary<Guid, string>());
            return new ScanResultDto(
                "CROSS_BRANCH",
                "This item belongs to another branch and cannot be counted in the current stocktake.",
                null,
                null,
                null,
                summary
            );
        }

        // 4. Expected Baseline Item Check
        var expectedLine = await _context.InventoryCountLines
            .FirstOrDefaultAsync(l => l.InventoryCountId == countId && l.InventoryItemId == item.Id, cancellationToken);

        if (expectedLine != null)
        {
            // Duplicate scan check
            if (expectedLine.IsCounted)
            {
                var summary = MapToDto(count, new Dictionary<Guid, string>());
                return new ScanResultDto(
                    "ALREADY_COUNTED",
                    $"Item '{expectedLine.TagNumberSnapshot}' ({expectedLine.ItemNameSnapshot}) was already counted at {expectedLine.CountedAt:HH:mm:ss}.",
                    MapLineToDto(expectedLine),
                    null,
                    null,
                    summary
                );
            }

            // Mark matched
            expectedLine.IsCounted = true;
            expectedLine.CountedAt = now;
            expectedLine.CountedBy = userId;
            expectedLine.Result = CountLineResult.Matched;

            count.CountedExpectedItemCount++;
            count.MatchedCount++;

            await _context.SaveChangesAsync(cancellationToken);

            var updatedSummary = MapToDto(count, new Dictionary<Guid, string>());
            return new ScanResultDto(
                "MATCHED",
                $"Matched: {expectedLine.TagNumberSnapshot} - {expectedLine.ItemNameSnapshot} ({expectedLine.GrossWeightSnapshot:F3}g)",
                MapLineToDto(expectedLine),
                null,
                null,
                updatedSummary
            );
        }

        // 5. Unexpected Item (Same branch, different location)
        var existingUnexpected = await _context.InventoryCountUnexpectedItems
            .FirstOrDefaultAsync(u => u.InventoryCountId == countId && u.InventoryItemId == item.Id, cancellationToken);

        if (existingUnexpected != null)
        {
            var summary = MapToDto(count, new Dictionary<Guid, string>());
            return new ScanResultDto(
                "ALREADY_COUNTED",
                $"Unexpected item '{existingUnexpected.TagNumberSnapshot}' was already scanned at {existingUnexpected.ScannedAt:HH:mm:ss}.",
                null,
                MapUnexpectedToDto(existingUnexpected),
                null,
                summary
            );
        }

        var unexpectedRecord = new InventoryCountUnexpectedItem
        {
            InventoryCountId = countId,
            InventoryItemId = item.Id,
            ObservedBranchId = count.BranchId,
            ObservedStockLocationId = count.StockLocationId,
            CurrentSystemStockLocationId = item.StockLocationId,
            CurrentSystemLocationNameSnapshot = item.StockLocation?.Name ?? "Unknown Location",
            TagNumberSnapshot = item.TagNumber,
            BarcodeSnapshot = item.Barcode,
            ItemNameSnapshot = item.Name,
            MaterialNameSnapshot = item.Material?.Name ?? string.Empty,
            CategoryNameSnapshot = item.Category?.Name ?? string.Empty,
            PurityNameSnapshot = item.Purity?.Name ?? string.Empty,
            GrossWeightSnapshot = item.GrossWeight,
            StoneWeightSnapshot = item.StoneWeight,
            NetWeightSnapshot = item.NetWeight,
            PieceCountSnapshot = item.PieceCount,
            CurrentSystemStatusSnapshot = item.Status.ToString().ToUpperInvariant(),
            ScannedAt = now,
            ScannedBy = userId,
            ResolutionStatus = VarianceResolutionStatus.Unresolved,
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.InventoryCountUnexpectedItems.Add(unexpectedRecord);
        count.UnexpectedCount++;

        RecordAudit("InventoryCount", count.Id, "UNEXPECTED_ITEM_FOUND",
            null,
            new { ItemId = item.Id, item.TagNumber, CurrentLocation = item.StockLocation?.Name, ObservedLocation = count.StockLocation?.Name },
            count.BranchId);

        await _context.SaveChangesAsync(cancellationToken);

        var finalSummary = MapToDto(count, new Dictionary<Guid, string>());
        return new ScanResultDto(
            "UNEXPECTED",
            $"Unexpected: '{item.TagNumber}' ({item.Name}) is currently recorded in location '{unexpectedRecord.CurrentSystemLocationNameSnapshot}'. Recorded as unexpected observation.",
            null,
            MapUnexpectedToDto(unexpectedRecord),
            null,
            finalSummary
        );
    }

    public async Task<ConfirmCompositionScanResultDto> ConfirmCompositionScanAsync(Guid countId, ConfirmCompositionScanRequest request, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        if (count.Status != InventoryCountStatus.InProgress)
        {
            throw new BusinessRuleException($"Cannot scan composition: inventory count is in '{count.Status.ToString().ToUpperInvariant()}' state.");
        }

        var composition = await _context.InventoryCompositions
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i.Category)
            .Include(c => c.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.InventoryItem)
                    .ThenInclude(i => i.Purity)
            .FirstOrDefaultAsync(c => c.Id == request.CompositionId && c.IsActive, cancellationToken);

        if (composition == null)
        {
            throw new NotFoundException("InventoryComposition", request.CompositionId);
        }

        var selectedIds = request.SelectedInventoryItemIds?.Distinct().ToList() ?? new List<Guid>();
        var activeMembers = composition.Members
            .Where(m => m.IsActive && m.InventoryItem != null && selectedIds.Contains(m.InventoryItemId))
            .ToList();

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;

        int newlyCounted = 0;
        int alreadyCounted = 0;
        int unexpected = 0;

        foreach (var member in activeMembers)
        {
            var item = member.InventoryItem!;

            // Check if in baseline
            var expectedLine = await _context.InventoryCountLines
                .FirstOrDefaultAsync(l => l.InventoryCountId == countId && l.InventoryItemId == item.Id, cancellationToken);

            if (expectedLine != null)
            {
                if (expectedLine.IsCounted)
                {
                    alreadyCounted++;
                }
                else
                {
                    expectedLine.IsCounted = true;
                    expectedLine.CountedAt = now;
                    expectedLine.CountedBy = userId;
                    expectedLine.Result = CountLineResult.Matched;
                    count.CountedExpectedItemCount++;
                    count.MatchedCount++;
                    newlyCounted++;
                }
            }
            else
            {
                // Unexpected item in composition
                var existingUnexpected = await _context.InventoryCountUnexpectedItems
                    .FirstOrDefaultAsync(u => u.InventoryCountId == countId && u.InventoryItemId == item.Id, cancellationToken);

                if (existingUnexpected != null)
                {
                    alreadyCounted++;
                }
                else
                {
                    var unexpectedRecord = new InventoryCountUnexpectedItem
                    {
                        InventoryCountId = countId,
                        InventoryItemId = item.Id,
                        ObservedBranchId = count.BranchId,
                        ObservedStockLocationId = count.StockLocationId,
                        CurrentSystemStockLocationId = item.StockLocationId,
                        CurrentSystemLocationNameSnapshot = "Different Location",
                        TagNumberSnapshot = item.TagNumber,
                        BarcodeSnapshot = item.Barcode,
                        ItemNameSnapshot = item.Name,
                        MaterialNameSnapshot = item.Material?.Name ?? string.Empty,
                        CategoryNameSnapshot = item.Category?.Name ?? string.Empty,
                        PurityNameSnapshot = item.Purity?.Name ?? string.Empty,
                        GrossWeightSnapshot = item.GrossWeight,
                        StoneWeightSnapshot = item.StoneWeight,
                        NetWeightSnapshot = item.NetWeight,
                        PieceCountSnapshot = item.PieceCount,
                        CurrentSystemStatusSnapshot = item.Status.ToString().ToUpperInvariant(),
                        ScannedAt = now,
                        ScannedBy = userId,
                        ResolutionStatus = VarianceResolutionStatus.Unresolved,
                        CreatedAt = now,
                        CreatedBy = userId
                    };

                    _context.InventoryCountUnexpectedItems.Add(unexpectedRecord);
                    count.UnexpectedCount++;
                    unexpected++;
                }
            }
        }

        // Invariant: The composition parent itself is NEVER counted as a stock unit!
        await _context.SaveChangesAsync(cancellationToken);

        var summary = MapToDto(count, new Dictionary<Guid, string>());
        return new ConfirmCompositionScanResultDto(
            newlyCounted,
            alreadyCounted,
            unexpected,
            $"Composition '{composition.Name}': {newlyCounted} physical items counted, {alreadyCounted} already counted, {unexpected} unexpected.",
            summary
        );
    }

    public async Task<InventoryCountDto> FinishCountAsync(Guid countId, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        if (count.Status != InventoryCountStatus.InProgress)
        {
            throw new BusinessRuleException($"Cannot finish count: count session is in '{count.Status.ToString().ToUpperInvariant()}' state.");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;

        // Atomically reconcile lines
        var uncountedLines = await _context.InventoryCountLines
            .Where(l => l.InventoryCountId == countId && !l.IsCounted)
            .ToListAsync(cancellationToken);

        foreach (var line in uncountedLines)
        {
            line.Result = CountLineResult.Missing;
            line.ResolutionStatus = VarianceResolutionStatus.Unresolved;
        }

        var countedLinesCount = await _context.InventoryCountLines
            .CountAsync(l => l.InventoryCountId == countId && l.IsCounted, cancellationToken);

        var unexpectedCount = await _context.InventoryCountUnexpectedItems
            .CountAsync(u => u.InventoryCountId == countId, cancellationToken);

        var missingCount = uncountedLines.Count;

        count.MatchedCount = countedLinesCount;
        count.CountedExpectedItemCount = countedLinesCount;
        count.MissingCount = missingCount;
        count.UnexpectedCount = unexpectedCount;
        count.ReconciledAt = now;
        count.ReconciledBy = userId;

        // Perfect Match Rule: If Missing = 0 and Unexpected = 0, automatically mark COMPLETED
        if (missingCount == 0 && unexpectedCount == 0)
        {
            count.Status = InventoryCountStatus.Completed;
            count.CompletedAt = now;
            count.CompletedBy = userId;

            RecordAudit("InventoryCount", count.Id, "COUNT_COMPLETED",
                null,
                new { count.BranchId, count.StockLocationId, count.ExpectedItemCount, count.MatchedCount, Result = "PERFECT_MATCH" },
                count.BranchId);
        }
        else
        {
            count.Status = InventoryCountStatus.RequiresReview;

            RecordAudit("InventoryCount", count.Id, "COUNT_RECONCILED",
                null,
                new { count.BranchId, count.StockLocationId, count.ExpectedItemCount, count.MatchedCount, MissingCount = missingCount, UnexpectedCount = unexpectedCount, Result = "REQUIRES_REVIEW" },
                count.BranchId);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetCountByIdAsync(count.Id, cancellationToken);
    }

    public async Task<InventoryCountDto> ResolveVarianceAsync(Guid countId, ResolveVarianceRequest request, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        if (count.Status != InventoryCountStatus.RequiresReview)
        {
            throw new BusinessRuleException($"Cannot resolve variance: count is in '{count.Status.ToString().ToUpperInvariant()}' state. Only counts in REQUIRES_REVIEW allow variance resolution.");
        }

        if (!VarianceReasonCodes.IsValid(request.ReasonCode))
        {
            throw new BusinessRuleException($"Invalid variance resolution reason code '{request.ReasonCode}'. Allowed codes: {string.Join(", ", VarianceReasonCodes.All)}.");
        }

        var normalizedCode = request.ReasonCode.Trim().ToUpperInvariant();
        if (normalizedCode == VarianceReasonCodes.Other && string.IsNullOrWhiteSpace(request.Notes))
        {
            throw new BusinessRuleException("Resolution notes are mandatory when reason code is 'OTHER'.");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;
        var varianceType = request.VarianceType?.Trim().ToLowerInvariant();

        if (varianceType == "missing")
        {
            var line = await _context.InventoryCountLines
                .FirstOrDefaultAsync(l => l.Id == request.VarianceId && l.InventoryCountId == countId, cancellationToken);

            if (line == null)
            {
                throw new NotFoundException("InventoryCountLine (Missing Variance)", request.VarianceId);
            }

            line.ResolutionStatus = VarianceResolutionStatus.Resolved;
            line.ResolutionReasonCode = normalizedCode;
            line.ResolutionNotes = request.Notes?.Trim();
            line.ResolvedAt = now;
            line.ResolvedBy = userId;

            // Invariant: Resolving a variance NEVER implicitly alters the underlying InventoryItem
            RecordAudit("InventoryCountLine", line.Id, "VARIANCE_RESOLVED",
                new { Status = "Unresolved" },
                new { Status = "Resolved", line.TagNumberSnapshot, Reason = normalizedCode, line.ResolutionNotes },
                count.BranchId);
        }
        else if (varianceType == "unexpected")
        {
            var unexpected = await _context.InventoryCountUnexpectedItems
                .FirstOrDefaultAsync(u => u.Id == request.VarianceId && u.InventoryCountId == countId, cancellationToken);

            if (unexpected == null)
            {
                throw new NotFoundException("InventoryCountUnexpectedItem (Unexpected Variance)", request.VarianceId);
            }

            unexpected.ResolutionStatus = VarianceResolutionStatus.Resolved;
            unexpected.ResolutionReasonCode = normalizedCode;
            unexpected.ResolutionNotes = request.Notes?.Trim();
            unexpected.ResolvedAt = now;
            unexpected.ResolvedBy = userId;

            // Invariant: Resolving a variance NEVER implicitly alters the underlying InventoryItem (e.g. StockLocationId remains untouched)
            RecordAudit("InventoryCountUnexpectedItem", unexpected.Id, "VARIANCE_RESOLVED",
                new { Status = "Unresolved" },
                new { Status = "Resolved", unexpected.TagNumberSnapshot, Reason = normalizedCode, unexpected.ResolutionNotes },
                count.BranchId);
        }
        else
        {
            throw new BusinessRuleException("VarianceType must be either 'Missing' or 'Unexpected'.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await GetCountByIdAsync(count.Id, cancellationToken);
    }

    public async Task<InventoryCountDto> CompleteCountAsync(Guid countId, CompleteInventoryCountRequest? request = null, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        if (count.Status != InventoryCountStatus.RequiresReview)
        {
            throw new BusinessRuleException($"Cannot complete count: session is in '{count.Status.ToString().ToUpperInvariant()}' state.");
        }

        // Verify that 100% of Missing variances are resolved
        var unresolvedMissingCount = await _context.InventoryCountLines
            .CountAsync(l => l.InventoryCountId == countId
                          && l.Result == CountLineResult.Missing
                          && l.ResolutionStatus == VarianceResolutionStatus.Unresolved,
                        cancellationToken);

        // Verify that 100% of Unexpected variances are resolved
        var unresolvedUnexpectedCount = await _context.InventoryCountUnexpectedItems
            .CountAsync(u => u.InventoryCountId == countId
                          && u.ResolutionStatus == VarianceResolutionStatus.Unresolved,
                        cancellationToken);

        if (unresolvedMissingCount > 0 || unresolvedUnexpectedCount > 0)
        {
            throw new BusinessRuleException($"Cannot complete inventory count: all variances must be resolved first. (Unresolved missing: {unresolvedMissingCount}, Unresolved unexpected: {unresolvedUnexpectedCount}).");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;

        count.Status = InventoryCountStatus.Completed;
        count.CompletedAt = now;
        count.CompletedBy = userId;
        if (!string.IsNullOrWhiteSpace(request?.Notes))
        {
            count.Notes = string.IsNullOrWhiteSpace(count.Notes)
                ? request.Notes.Trim()
                : $"{count.Notes}\nCompletion Notes: {request.Notes.Trim()}";
        }

        RecordAudit("InventoryCount", count.Id, "COUNT_COMPLETED",
            new { Status = "RequiresReview" },
            new { Status = "Completed", count.ExpectedItemCount, count.MatchedCount, count.MissingCount, count.UnexpectedCount },
            count.BranchId);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetCountByIdAsync(count.Id, cancellationToken);
    }

    public async Task<InventoryCountDto> CancelCountAsync(Guid countId, CancelInventoryCountRequest request, CancellationToken cancellationToken = default)
    {
        var count = await _context.InventoryCounts
            .Include(c => c.Branch)
            .Include(c => c.StockLocation)
            .FirstOrDefaultAsync(c => c.Id == countId, cancellationToken);

        if (count == null)
        {
            throw new NotFoundException("InventoryCount", countId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(count.BranchId, cancellationToken);

        if (count.Status == InventoryCountStatus.Completed)
        {
            throw new BusinessRuleException("Completed inventory counts are permanent historical records and cannot be cancelled.");
        }
        if (count.Status == InventoryCountStatus.Cancelled)
        {
            throw new BusinessRuleException("This inventory count is already cancelled.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("Cancellation reason is mandatory.");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTimeOffset.UtcNow;

        var oldStatus = count.Status.ToString();
        count.Status = InventoryCountStatus.Cancelled;
        count.CancelledAt = now;
        count.CancelledBy = userId;
        count.CancellationReason = request.Reason.Trim();

        RecordAudit("InventoryCount", count.Id, "COUNT_CANCELLED",
            new { Status = oldStatus },
            new { Status = "Cancelled", Reason = count.CancellationReason },
            count.BranchId);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetCountByIdAsync(count.Id, cancellationToken);
    }

    private static InventoryCountDto MapToDto(InventoryCount count, Dictionary<Guid, string> userLookup)
    {
        userLookup.TryGetValue(count.StartedBy, out var startedByName);
        string? reconciledByName = null;
        if (count.ReconciledBy.HasValue) userLookup.TryGetValue(count.ReconciledBy.Value, out reconciledByName);
        string? completedByName = null;
        if (count.CompletedBy.HasValue) userLookup.TryGetValue(count.CompletedBy.Value, out completedByName);

        decimal progress = count.ExpectedItemCount > 0
            ? Math.Round((decimal)count.CountedExpectedItemCount / count.ExpectedItemCount * 100m, 2)
            : 0m;

        return new InventoryCountDto(
            count.Id,
            count.BranchId,
            count.Branch?.Code ?? string.Empty,
            count.Branch?.Name ?? string.Empty,
            count.StockLocationId,
            count.StockLocation?.Code ?? string.Empty,
            count.StockLocation?.Name ?? string.Empty,
            (int)count.Status,
            FormatStatusName(count.Status),
            count.StartedAt,
            count.StartedBy,
            startedByName,
            count.ReconciledAt,
            count.ReconciledBy,
            reconciledByName,
            count.CompletedAt,
            count.CompletedBy,
            completedByName,
            count.CancelledAt,
            count.CancelledBy,
            count.CancellationReason,
            count.ExpectedItemCount,
            count.CountedExpectedItemCount,
            count.MatchedCount,
            count.MissingCount,
            count.UnexpectedCount,
            progress,
            count.Notes,
            count.Version
        );
    }

    private static InventoryCountLineDto MapLineToDto(InventoryCountLine l)
    {
        return new InventoryCountLineDto(
            l.Id,
            l.InventoryCountId,
            l.InventoryItemId,
            l.ExpectedBranchId,
            l.ExpectedStockLocationId,
            l.TagNumberSnapshot,
            l.BarcodeSnapshot,
            l.ItemNameSnapshot,
            l.MaterialNameSnapshot,
            l.CategoryNameSnapshot,
            l.PurityNameSnapshot,
            l.GrossWeightSnapshot,
            l.StoneWeightSnapshot,
            l.NetWeightSnapshot,
            l.PieceCountSnapshot,
            l.ExpectedStatusSnapshot,
            l.IsExpected,
            l.IsCounted,
            l.CountedAt,
            l.CountedBy,
            (int)l.Result,
            l.Result.ToString().ToUpperInvariant(),
            (int)l.ResolutionStatus,
            l.ResolutionStatus.ToString().ToUpperInvariant(),
            l.ResolutionReasonCode,
            l.ResolutionNotes,
            l.ResolvedAt,
            l.ResolvedBy
        );
    }

    private static InventoryCountUnexpectedItemDto MapUnexpectedToDto(InventoryCountUnexpectedItem u)
    {
        return new InventoryCountUnexpectedItemDto(
            u.Id,
            u.InventoryCountId,
            u.InventoryItemId,
            u.ObservedBranchId,
            u.ObservedStockLocationId,
            u.CurrentSystemStockLocationId,
            u.CurrentSystemLocationNameSnapshot,
            u.TagNumberSnapshot,
            u.BarcodeSnapshot,
            u.ItemNameSnapshot,
            u.MaterialNameSnapshot,
            u.CategoryNameSnapshot,
            u.PurityNameSnapshot,
            u.GrossWeightSnapshot,
            u.StoneWeightSnapshot,
            u.NetWeightSnapshot,
            u.PieceCountSnapshot,
            u.CurrentSystemStatusSnapshot,
            u.ScannedAt,
            u.ScannedBy,
            (int)u.ResolutionStatus,
            u.ResolutionStatus.ToString().ToUpperInvariant(),
            u.ResolutionReasonCode,
            u.ResolutionNotes,
            u.ResolvedAt,
            u.ResolvedBy
        );
    }

    private void RecordAudit(string entityName, Guid entityId, string action, object? oldValues, object? newValues, Guid? branchId)
    {
        var audit = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Action = action,
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = branchId ?? _currentUserService.BranchId,
            Timestamp = DateTimeOffset.UtcNow,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent
        };

        _context.AuditLogs.Add(audit);
    }

    private static string FormatStatusName(InventoryCountStatus status) => status switch
    {
        InventoryCountStatus.InProgress => "IN_PROGRESS",
        InventoryCountStatus.RequiresReview => "REQUIRES_REVIEW",
        InventoryCountStatus.Completed => "COMPLETED",
        InventoryCountStatus.Cancelled => "CANCELLED",
        _ => status.ToString().ToUpperInvariant()
    };
}

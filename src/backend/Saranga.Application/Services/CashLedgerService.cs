using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.CashLedger;
using Saranga.Application.DTOs.Common;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public class CashLedgerService : ICashLedgerService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITagGeneratorService _tagGenerator;
    private readonly IShiftExpectedBalanceService _expectedBalanceService;
    private readonly ILogger<CashLedgerService> _logger;

    public CashLedgerService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        ITagGeneratorService tagGenerator,
        IShiftExpectedBalanceService expectedBalanceService,
        ILogger<CashLedgerService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _tagGenerator = tagGenerator;
        _expectedBalanceService = expectedBalanceService;
        _logger = logger;
    }

    public async Task<PagedResult<CashLedgerTransactionDto>> GetTransactionsAsync(CashLedgerTransactionFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.CashLedgerTransactions
            .AsNoTracking()
            .Include(t => t.Branch)
            .Include(t => t.Category)
            .Include(t => t.ReversalOfTransaction)
            .Include(t => t.Creator)
            .Include(t => t.Entries)
                .ThenInclude(e => e.CashDrawer)
            .Include(t => t.Entries)
                .ThenInclude(e => e.Shift)
            .Include(t => t.Entries)
                .ThenInclude(e => e.Creator)
            .Where(t => !t.IsDeleted);

        // Branch authorization check
        if (!_currentUserService.IsGlobalAdmin)
        {
            query = query.Where(t => _currentUserService.AccessibleBranchIds.Contains(t.BranchId));
        }

        if (filter.BranchId.HasValue)
        {
            query = query.Where(t => t.BranchId == filter.BranchId.Value);
        }

        if (filter.TransactionType.HasValue)
        {
            query = query.Where(t => (int)t.TransactionType == filter.TransactionType.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == filter.CategoryId.Value);
        }

        if (filter.UserId.HasValue)
        {
            query = query.Where(t => t.CreatedBy == filter.UserId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(t => (int)t.Status == filter.Status.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(t => t.TransactionAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(t => t.TransactionAt <= filter.ToDate.Value);
        }

        if (filter.CashDrawerId.HasValue)
        {
            query = query.Where(t => t.Entries.Any(e => e.CashDrawerId == filter.CashDrawerId.Value));
        }

        if (filter.ShiftId.HasValue)
        {
            query = query.Where(t => t.Entries.Any(e => e.ShiftId == filter.ShiftId.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 50 : (filter.PageSize > 100 ? 100 : filter.PageSize);

        var items = await query
            .OrderByDescending(t => t.TransactionAt)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToTransactionDto).ToList();
        return new PagedResult<CashLedgerTransactionDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<CashLedgerTransactionDto> GetTransactionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tx = await _context.CashLedgerTransactions
            .AsNoTracking()
            .Include(t => t.Branch)
            .Include(t => t.Category)
            .Include(t => t.ReversalOfTransaction)
            .Include(t => t.Creator)
            .Include(t => t.Entries)
                .ThenInclude(e => e.CashDrawer)
            .Include(t => t.Entries)
                .ThenInclude(e => e.Shift)
            .Include(t => t.Entries)
                .ThenInclude(e => e.Creator)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("CashLedgerTransaction", id);

        if (!_currentUserService.IsGlobalAdmin && !_currentUserService.AccessibleBranchIds.Contains(tx.BranchId))
        {
            throw new ForbiddenException("You are not authorized to view transactions for this branch.");
        }

        return MapToTransactionDto(tx);
    }

    public async Task<CashLedgerTransactionDto> PostCashInAsync(PostCashInRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ValidateMonetaryAmount(request.Amount);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessRuleException("An 'Idempotency-Key' request header is required for this financial posting operation.");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var normalizedKey = idempotencyKey.Trim();

        // 1. Idempotency Check (Fast path)
        var payloadJson = JsonSerializer.Serialize(request);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));

        var existingRecord = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(existingRecord.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        // 2. Atomic Database Transaction with PostgreSQL row-level lock
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Shifts\" WHERE \"Id\" = {request.ShiftId} FOR UPDATE", cancellationToken);

        // Under-lock idempotency re-check
        var recordUnderLock = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (recordUnderLock != null)
        {
            if (recordUnderLock.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(recordUnderLock.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        var shift = await _context.Shifts
            .Include(s => s.CashDrawer)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken)
            ?? throw new NotFoundException("Shift", request.ShiftId);

        ValidateShiftForPosting(shift);

        CashMovementCategory? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await _context.CashMovementCategories
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value && !c.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("CashMovementCategory", request.CategoryId.Value);

            if (!category.IsActive)
            {
                throw new BusinessRuleException($"Category '{category.Name}' is deactivated and cannot be selected.");
            }

            if (category.AllowedDirection != CashCategoryDirection.In && category.AllowedDirection != CashCategoryDirection.Both)
            {
                throw new BusinessRuleException($"Category '{category.Name}' cannot be used for Cash In movements.");
            }
        }

        var txRef = await _tagGenerator.GenerateCashLedgerReferenceAsync(shift.Branch?.Code ?? "BR", cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var tx = new CashLedgerTransaction
        {
            Id = Guid.NewGuid(),
            TransactionReference = txRef,
            BranchId = shift.BranchId,
            TransactionType = CashLedgerTransactionType.CashIn,
            SourceType = CashLedgerSourceType.Manual,
            SourceReferenceId = string.IsNullOrWhiteSpace(request.ExternalReference) ? null : request.ExternalReference.Trim(),
            Amount = request.Amount,
            CurrencyCode = shift.CurrencyCode,
            Description = request.Description?.Trim(),
            Reason = request.Reason?.Trim(),
            CategoryId = request.CategoryId,
            CategoryNameSnapshot = category?.Name,
            CategoryCodeSnapshot = category?.Code,
            Status = CashLedgerStatus.Posted,
            TransactionAt = now,
            IdempotencyKey = normalizedKey,
            CreatedAt = now,
            CreatedBy = userId
        };

        var entry = new CashLedgerEntry
        {
            Id = Guid.NewGuid(),
            CashLedgerTransactionId = tx.Id,
            BranchId = shift.BranchId,
            CashDrawerId = shift.CashDrawerId,
            ShiftId = shift.Id,
            Direction = CashLedgerEntryDirection.In,
            Amount = request.Amount,
            CurrencyCode = shift.CurrencyCode,
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.CashLedgerTransactions.Add(tx);
        _context.CashLedgerEntries.Add(entry);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "CashLedgerTransaction",
            EntityId = tx.Id.ToString(),
            Action = "CASH_IN_POSTED",
            UserId = userId,
            Timestamp = now,
            NewValues = JsonSerializer.Serialize(new
            {
                tx.TransactionReference,
                tx.TransactionType,
                tx.Amount,
                tx.CurrencyCode,
                ShiftId = shift.Id,
                ShiftNumber = shift.ShiftNumber,
                DrawerId = shift.CashDrawerId,
                DrawerName = shift.CashDrawer?.Name,
                CategoryId = category?.Id,
                CategoryName = category?.Name,
                Reason = tx.Reason
            })
        };
        _context.AuditLogs.Add(auditLog);

        var resultDto = new CashLedgerTransactionDto(
            tx.Id,
            tx.TransactionReference,
            tx.BranchId,
            shift.Branch?.Name,
            (int)tx.TransactionType,
            "Cash In",
            (int)tx.SourceType,
            "Manual",
            tx.SourceReferenceId,
            tx.Amount,
            tx.CurrencyCode,
            tx.Description,
            tx.Reason,
            tx.CategoryId,
            category?.Name,
            category?.Code,
            (int)tx.Status,
            "Posted",
            tx.TransactionAt,
            null,
            null,
            tx.CreatedAt,
            userId,
            _currentUserService.Username,
            new List<CashLedgerEntryDto>
            {
                new(
                    entry.Id,
                    tx.Id,
                    entry.BranchId,
                    entry.CashDrawerId,
                    shift.CashDrawer?.Name,
                    shift.CashDrawer?.DrawerCode,
                    entry.ShiftId,
                    shift.ShiftNumber,
                    (int)entry.Direction,
                    "In",
                    entry.Amount,
                    entry.CurrencyCode,
                    entry.CreatedAt,
                    userId,
                    _currentUserService.Username
                )
            }
        );

        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = normalizedKey,
            Endpoint = "/api/cash-ledger/cash-in",
            RequestHash = requestHash,
            ResponseBody = JsonSerializer.Serialize(resultDto),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);

            var committedRecord = await _context.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

            if (committedRecord != null)
            {
                if (committedRecord.RequestHash != requestHash)
                {
                    throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
                }

                var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(committedRecord.ResponseBody);
                if (cachedDto != null)
                {
                    return cachedDto;
                }
            }

            throw;
        }

        _logger.LogInformation("Posted Cash In {TxRef} of {Amount:N2} {Currency} to Shift {ShiftNumber} (Drawer: {DrawerName}) by User {UserId}",
            tx.TransactionReference, tx.Amount, tx.CurrencyCode, shift.ShiftNumber, shift.CashDrawer?.Name, userId);

        return resultDto;
    }

    public async Task<CashLedgerTransactionDto> PostCashOutAsync(PostCashOutRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ValidateMonetaryAmount(request.Amount);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessRuleException("An 'Idempotency-Key' request header is required for this financial posting operation.");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var normalizedKey = idempotencyKey.Trim();

        // 1. Idempotency Check (Fast path)
        var payloadJson = JsonSerializer.Serialize(request);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));

        var existingRecord = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(existingRecord.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        // 2. Atomic Database Transaction with PostgreSQL row-level lock
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Shifts\" WHERE \"Id\" = {request.ShiftId} FOR UPDATE", cancellationToken);

        // Under-lock idempotency re-check
        var recordUnderLock = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (recordUnderLock != null)
        {
            if (recordUnderLock.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(recordUnderLock.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        var shift = await _context.Shifts
            .Include(s => s.CashDrawer)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken)
            ?? throw new NotFoundException("Shift", request.ShiftId);

        ValidateShiftForPosting(shift);

        CashMovementCategory? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await _context.CashMovementCategories
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value && !c.IsDeleted, cancellationToken)
                ?? throw new NotFoundException("CashMovementCategory", request.CategoryId.Value);

            if (!category.IsActive)
            {
                throw new BusinessRuleException($"Category '{category.Name}' is deactivated and cannot be selected.");
            }

            if (category.AllowedDirection != CashCategoryDirection.Out && category.AllowedDirection != CashCategoryDirection.Both)
            {
                throw new BusinessRuleException($"Category '{category.Name}' cannot be used for Cash Out movements.");
            }
        }

        // Section 10: Insufficient Expected Cash validation
        var currentExpected = await _expectedBalanceService.CalculateExpectedClosingBalanceAsync(shift.Id, shift.OpeningBalance, cancellationToken);
        if (currentExpected < request.Amount)
        {
            throw new BusinessRuleException($"Insufficient expected cash in drawer '{shift.CashDrawer?.Name}'. Current balance is {shift.CurrencyCode} {currentExpected:N2}, requested cash out is {shift.CurrencyCode} {request.Amount:N2}. Negative drawer balances are not permitted.");
        }

        var txRef = await _tagGenerator.GenerateCashLedgerReferenceAsync(shift.Branch?.Code ?? "BR", cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var tx = new CashLedgerTransaction
        {
            Id = Guid.NewGuid(),
            TransactionReference = txRef,
            BranchId = shift.BranchId,
            TransactionType = CashLedgerTransactionType.CashOut,
            SourceType = CashLedgerSourceType.Manual,
            SourceReferenceId = string.IsNullOrWhiteSpace(request.ExternalReference) ? null : request.ExternalReference.Trim(),
            Amount = request.Amount,
            CurrencyCode = shift.CurrencyCode,
            Description = request.Description?.Trim(),
            Reason = request.Reason?.Trim(),
            CategoryId = request.CategoryId,
            CategoryNameSnapshot = category?.Name,
            CategoryCodeSnapshot = category?.Code,
            Status = CashLedgerStatus.Posted,
            TransactionAt = now,
            IdempotencyKey = normalizedKey,
            CreatedAt = now,
            CreatedBy = userId
        };

        var entry = new CashLedgerEntry
        {
            Id = Guid.NewGuid(),
            CashLedgerTransactionId = tx.Id,
            BranchId = shift.BranchId,
            CashDrawerId = shift.CashDrawerId,
            ShiftId = shift.Id,
            Direction = CashLedgerEntryDirection.Out,
            Amount = request.Amount,
            CurrencyCode = shift.CurrencyCode,
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.CashLedgerTransactions.Add(tx);
        _context.CashLedgerEntries.Add(entry);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "CashLedgerTransaction",
            EntityId = tx.Id.ToString(),
            Action = "CASH_OUT_POSTED",
            UserId = userId,
            Timestamp = now,
            NewValues = JsonSerializer.Serialize(new
            {
                tx.TransactionReference,
                tx.TransactionType,
                tx.Amount,
                tx.CurrencyCode,
                ShiftId = shift.Id,
                ShiftNumber = shift.ShiftNumber,
                DrawerId = shift.CashDrawerId,
                DrawerName = shift.CashDrawer?.Name,
                CategoryId = category?.Id,
                CategoryName = category?.Name,
                Reason = tx.Reason,
                ExpectedCashBefore = currentExpected,
                ExpectedCashAfter = currentExpected - request.Amount
            })
        };
        _context.AuditLogs.Add(auditLog);

        var resultDto = new CashLedgerTransactionDto(
            tx.Id,
            tx.TransactionReference,
            tx.BranchId,
            shift.Branch?.Name,
            (int)tx.TransactionType,
            "Cash Out",
            (int)tx.SourceType,
            "Manual",
            tx.SourceReferenceId,
            tx.Amount,
            tx.CurrencyCode,
            tx.Description,
            tx.Reason,
            tx.CategoryId,
            category?.Name,
            category?.Code,
            (int)tx.Status,
            "Posted",
            tx.TransactionAt,
            null,
            null,
            tx.CreatedAt,
            userId,
            _currentUserService.Username,
            new List<CashLedgerEntryDto>
            {
                new(
                    entry.Id,
                    tx.Id,
                    entry.BranchId,
                    entry.CashDrawerId,
                    shift.CashDrawer?.Name,
                    shift.CashDrawer?.DrawerCode,
                    entry.ShiftId,
                    shift.ShiftNumber,
                    (int)entry.Direction,
                    "Out",
                    entry.Amount,
                    entry.CurrencyCode,
                    entry.CreatedAt,
                    userId,
                    _currentUserService.Username
                )
            }
        );

        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = normalizedKey,
            Endpoint = "/api/cash-ledger/cash-out",
            RequestHash = requestHash,
            ResponseBody = JsonSerializer.Serialize(resultDto),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);

            var committedRecord = await _context.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

            if (committedRecord != null)
            {
                if (committedRecord.RequestHash != requestHash)
                {
                    throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
                }

                var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(committedRecord.ResponseBody);
                if (cachedDto != null)
                {
                    return cachedDto;
                }
            }

            throw;
        }

        _logger.LogInformation("Posted Cash Out {TxRef} of {Amount:N2} {Currency} from Shift {ShiftNumber} (Drawer: {DrawerName}) by User {UserId}. Expected Remaining: {Remaining:N2}",
            tx.TransactionReference, tx.Amount, tx.CurrencyCode, shift.ShiftNumber, shift.CashDrawer?.Name, userId, currentExpected - request.Amount);

        return resultDto;
    }

    public async Task<CashLedgerTransactionDto> PostTransferAsync(PostTransferRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ValidateMonetaryAmount(request.Amount);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessRuleException("An 'Idempotency-Key' request header is required for this financial posting operation.");
        }

        if (request.SourceShiftId == request.DestinationShiftId)
        {
            throw new BusinessRuleException("Source and destination shifts cannot be the same.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("A transfer reason is mandatory.");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var normalizedKey = idempotencyKey.Trim();

        // 1. Idempotency Check (Fast path)
        var payloadJson = JsonSerializer.Serialize(request);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));

        var existingRecord = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(existingRecord.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        // 2. Atomic Database Transaction with Deterministic Locking (Section 14)
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Under-lock idempotency re-check
        var recordUnderLock = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (recordUnderLock != null)
        {
            if (recordUnderLock.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(recordUnderLock.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        var (firstId, secondId) = request.SourceShiftId.CompareTo(request.DestinationShiftId) < 0
            ? (request.SourceShiftId, request.DestinationShiftId)
            : (request.DestinationShiftId, request.SourceShiftId);

        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Shifts\" WHERE \"Id\" = {firstId} FOR UPDATE", cancellationToken);
        var firstShift = await _context.Shifts
            .Include(s => s.CashDrawer)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == firstId, cancellationToken)
            ?? throw new NotFoundException("Shift", firstId);

        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Shifts\" WHERE \"Id\" = {secondId} FOR UPDATE", cancellationToken);
        var secondShift = await _context.Shifts
            .Include(s => s.CashDrawer)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == secondId, cancellationToken)
            ?? throw new NotFoundException("Shift", secondId);

        var sourceShift = request.SourceShiftId == firstId ? firstShift : secondShift;
        var destShift = request.DestinationShiftId == firstId ? firstShift : secondShift;

        // Validation (Section 13)
        ValidateShiftForPosting(sourceShift, isTransfer: true);
        ValidateShiftForPosting(destShift, isTransfer: true);

        if (sourceShift.CashDrawerId == destShift.CashDrawerId)
        {
            throw new BusinessRuleException("Cannot transfer cash to the same cash drawer.");
        }

        if (sourceShift.BranchId != destShift.BranchId)
        {
            throw new BusinessRuleException("Cross-branch cash transfers are not supported. Both drawers must belong to the same branch.");
        }

        if (!string.Equals(sourceShift.CurrencyCode, destShift.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException($"Currency mismatch between shifts ({sourceShift.CurrencyCode} vs {destShift.CurrencyCode}).");
        }

        // Validate source drawer has sufficient cash
        var sourceExpected = await _expectedBalanceService.CalculateExpectedClosingBalanceAsync(sourceShift.Id, sourceShift.OpeningBalance, cancellationToken);
        if (sourceExpected < request.Amount)
        {
            throw new BusinessRuleException($"Insufficient expected cash in source drawer '{sourceShift.CashDrawer?.Name}'. Current balance is {sourceShift.CurrencyCode} {sourceExpected:N2}, requested transfer is {sourceShift.CurrencyCode} {request.Amount:N2}.");
        }

        var txRef = await _tagGenerator.GenerateCashLedgerReferenceAsync(sourceShift.Branch?.Code ?? "BR", cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var tx = new CashLedgerTransaction
        {
            Id = Guid.NewGuid(),
            TransactionReference = txRef,
            BranchId = sourceShift.BranchId,
            TransactionType = CashLedgerTransactionType.Transfer,
            SourceType = CashLedgerSourceType.Manual,
            SourceReferenceId = string.IsNullOrWhiteSpace(request.ExternalReference) ? null : request.ExternalReference.Trim(),
            Amount = request.Amount,
            CurrencyCode = sourceShift.CurrencyCode,
            Description = $"Physical drawer cash transfer from '{sourceShift.CashDrawer?.Name}' ({sourceShift.ShiftNumber}) to '{destShift.CashDrawer?.Name}' ({destShift.ShiftNumber})",
            Reason = request.Reason.Trim(),
            Status = CashLedgerStatus.Posted,
            TransactionAt = now,
            IdempotencyKey = normalizedKey,
            CreatedAt = now,
            CreatedBy = userId
        };

        var sourceOutEntry = new CashLedgerEntry
        {
            Id = Guid.NewGuid(),
            CashLedgerTransactionId = tx.Id,
            BranchId = sourceShift.BranchId,
            CashDrawerId = sourceShift.CashDrawerId,
            ShiftId = sourceShift.Id,
            Direction = CashLedgerEntryDirection.Out,
            Amount = request.Amount,
            CurrencyCode = sourceShift.CurrencyCode,
            CreatedAt = now,
            CreatedBy = userId
        };

        var destInEntry = new CashLedgerEntry
        {
            Id = Guid.NewGuid(),
            CashLedgerTransactionId = tx.Id,
            BranchId = destShift.BranchId,
            CashDrawerId = destShift.CashDrawerId,
            ShiftId = destShift.Id,
            Direction = CashLedgerEntryDirection.In,
            Amount = request.Amount,
            CurrencyCode = destShift.CurrencyCode,
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.CashLedgerTransactions.Add(tx);
        _context.CashLedgerEntries.Add(sourceOutEntry);
        _context.CashLedgerEntries.Add(destInEntry);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "CashLedgerTransaction",
            EntityId = tx.Id.ToString(),
            Action = "CASH_TRANSFER_POSTED",
            UserId = userId,
            Timestamp = now,
            NewValues = JsonSerializer.Serialize(new
            {
                tx.TransactionReference,
                tx.Amount,
                tx.CurrencyCode,
                SourceShiftId = sourceShift.Id,
                SourceShiftNumber = sourceShift.ShiftNumber,
                SourceDrawerId = sourceShift.CashDrawerId,
                SourceDrawerName = sourceShift.CashDrawer?.Name,
                DestinationShiftId = destShift.Id,
                DestinationShiftNumber = destShift.ShiftNumber,
                DestinationDrawerId = destShift.CashDrawerId,
                DestinationDrawerName = destShift.CashDrawer?.Name,
                tx.Reason
            })
        };
        _context.AuditLogs.Add(auditLog);

        var resultDto = new CashLedgerTransactionDto(
            tx.Id,
            tx.TransactionReference,
            tx.BranchId,
            sourceShift.Branch?.Name,
            (int)tx.TransactionType,
            "Transfer",
            (int)tx.SourceType,
            "Manual",
            tx.SourceReferenceId,
            tx.Amount,
            tx.CurrencyCode,
            tx.Description,
            tx.Reason,
            null,
            null,
            null,
            (int)tx.Status,
            "Posted",
            tx.TransactionAt,
            null,
            null,
            tx.CreatedAt,
            userId,
            _currentUserService.Username,
            new List<CashLedgerEntryDto>
            {
                new(
                    sourceOutEntry.Id,
                    tx.Id,
                    sourceOutEntry.BranchId,
                    sourceOutEntry.CashDrawerId,
                    sourceShift.CashDrawer?.Name,
                    sourceShift.CashDrawer?.DrawerCode,
                    sourceOutEntry.ShiftId,
                    sourceShift.ShiftNumber,
                    (int)sourceOutEntry.Direction,
                    "Out",
                    sourceOutEntry.Amount,
                    sourceOutEntry.CurrencyCode,
                    sourceOutEntry.CreatedAt,
                    userId,
                    _currentUserService.Username
                ),
                new(
                    destInEntry.Id,
                    tx.Id,
                    destInEntry.BranchId,
                    destInEntry.CashDrawerId,
                    destShift.CashDrawer?.Name,
                    destShift.CashDrawer?.DrawerCode,
                    destInEntry.ShiftId,
                    destShift.ShiftNumber,
                    (int)destInEntry.Direction,
                    "In",
                    destInEntry.Amount,
                    destInEntry.CurrencyCode,
                    destInEntry.CreatedAt,
                    userId,
                    _currentUserService.Username
                )
            }
        );

        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = normalizedKey,
            Endpoint = "/api/cash-ledger/transfer",
            RequestHash = requestHash,
            ResponseBody = JsonSerializer.Serialize(resultDto),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);

            var committedRecord = await _context.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

            if (committedRecord != null)
            {
                if (committedRecord.RequestHash != requestHash)
                {
                    throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
                }

                var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(committedRecord.ResponseBody);
                if (cachedDto != null)
                {
                    return cachedDto;
                }
            }

            throw;
        }

        _logger.LogInformation("Posted Cash Transfer {TxRef} of {Amount:N2} {Currency} from Drawer {SourceDrawer} to Drawer {DestDrawer} by User {UserId}",
            tx.TransactionReference, tx.Amount, tx.CurrencyCode, sourceShift.CashDrawer?.Name, destShift.CashDrawer?.Name, userId);

        return resultDto;
    }

    public async Task<CashLedgerTransactionDto> ReverseTransactionAsync(Guid transactionId, ReverseTransactionRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessRuleException("An 'Idempotency-Key' request header is required for this financial posting operation.");
        }

        if (string.IsNullOrWhiteSpace(request.ReversalReason))
        {
            throw new BusinessRuleException("A reversal reason is mandatory.");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var normalizedKey = idempotencyKey.Trim();

        // 1. Idempotency Check (Fast path)
        var payloadJson = JsonSerializer.Serialize(request);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));

        var existingRecord = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(existingRecord.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        // 2. Atomic Database Transaction with Locking
        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Under-lock idempotency re-check
        var recordUnderLock = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (recordUnderLock != null)
        {
            if (recordUnderLock.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(recordUnderLock.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        var orig = await _context.CashLedgerTransactions
            .Include(t => t.Entries)
                .ThenInclude(e => e.CashDrawer)
            .Include(t => t.Entries)
                .ThenInclude(e => e.Shift)
            .Include(t => t.Branch)
            .FirstOrDefaultAsync(t => t.Id == transactionId && !t.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("CashLedgerTransaction", transactionId);

        // Branch check
        if (!_currentUserService.IsGlobalAdmin && !_currentUserService.AccessibleBranchIds.Contains(orig.BranchId))
        {
            throw new ForbiddenException("You are not authorized to reverse transactions for this branch.");
        }

        // Verification (Sections 18-21)
        if (orig.Status == CashLedgerStatus.Reversed)
        {
            throw new BusinessRuleException($"Transaction '{orig.TransactionReference}' has already been reversed.");
        }

        if (orig.TransactionType == CashLedgerTransactionType.Reversal)
        {
            throw new BusinessRuleException("Reversal transactions cannot be reversed.");
        }

        if (orig.SourceType != CashLedgerSourceType.Manual)
        {
            throw new BusinessRuleException($"Transactions originated from '{orig.SourceType}' cannot be reversed through the manual cash ledger workflow.");
        }

        var alreadyReversed = await _context.CashLedgerTransactions
            .AnyAsync(t => t.ReversalOfTransactionId == orig.Id, cancellationToken);
        if (alreadyReversed)
        {
            throw new BusinessRuleException($"Transaction '{orig.TransactionReference}' has already been reversed.");
        }

        // Lock all affected shifts and check that they are still OPEN (Section 20)
        var shiftIds = orig.Entries.Select(e => e.ShiftId).Distinct().OrderBy(id => id).ToList();
        var lockedShifts = new Dictionary<Guid, Shift>();

        foreach (var shiftId in shiftIds)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Shifts\" WHERE \"Id\" = {shiftId} FOR UPDATE", cancellationToken);
            var shift = await _context.Shifts
                .Include(s => s.CashDrawer)
                .Include(s => s.Branch)
                .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken);

            if (shift == null || shift.Status != ShiftStatus.Open)
            {
                throw new BusinessRuleException($"Cannot reverse transaction '{orig.TransactionReference}' because affected shift '{(shift?.ShiftNumber ?? shiftId.ToString())}' is closed. Closed shift transactions cannot be reversed.");
            }

            if (!shift.CashDrawer?.IsActive ?? true)
            {
                throw new BusinessRuleException($"Cannot reverse transaction: Cash drawer '{shift.CashDrawer?.Name}' is inactive.");
            }

            lockedShifts[shiftId] = shift;
        }

        // Validate sufficient cash for compensating Out entries
        foreach (var entry in orig.Entries)
        {
            if (entry.Direction == CashLedgerEntryDirection.In)
            {
                // Compensating direction is Out! Drawer must have enough cash.
                var shift = lockedShifts[entry.ShiftId];
                var expected = await _expectedBalanceService.CalculateExpectedClosingBalanceAsync(shift.Id, shift.OpeningBalance, cancellationToken);
                if (expected < entry.Amount)
                {
                    throw new BusinessRuleException($"Cannot reverse transaction: Insufficient cash in drawer '{shift.CashDrawer?.Name}' to process compensating cash-out of {shift.CurrencyCode} {entry.Amount:N2}. Current balance is {expected:N2}.");
                }
            }
        }

        var now = DateTimeOffset.UtcNow;
        var revRef = await _tagGenerator.GenerateCashLedgerReferenceAsync(orig.Branch?.Code ?? "BR", cancellationToken);

        // Mark original transaction as Reversed (Section 18 & 21)
        orig.Status = CashLedgerStatus.Reversed;
        orig.UpdatedAt = now;
        orig.UpdatedBy = userId;

        // Create Compensating Reversal Transaction
        var revTx = new CashLedgerTransaction
        {
            Id = Guid.NewGuid(),
            TransactionReference = revRef,
            BranchId = orig.BranchId,
            TransactionType = CashLedgerTransactionType.Reversal,
            SourceType = CashLedgerSourceType.Manual,
            Amount = orig.Amount,
            CurrencyCode = orig.CurrencyCode,
            Description = $"Reversal of {orig.TransactionReference}: {request.ReversalReason.Trim()}",
            Reason = request.ReversalReason.Trim(),
            CategoryId = orig.CategoryId,
            CategoryNameSnapshot = orig.CategoryNameSnapshot ?? orig.Category?.Name,
            CategoryCodeSnapshot = orig.CategoryCodeSnapshot ?? orig.Category?.Code,
            Status = CashLedgerStatus.Posted,
            TransactionAt = now,
            ReversalOfTransactionId = orig.Id,
            IdempotencyKey = normalizedKey,
            CreatedAt = now,
            CreatedBy = userId
        };

        var compensatingEntries = new List<CashLedgerEntry>();
        var entryDtos = new List<CashLedgerEntryDto>();

        foreach (var origEntry in orig.Entries)
        {
            var compDirection = origEntry.Direction == CashLedgerEntryDirection.In
                ? CashLedgerEntryDirection.Out
                : CashLedgerEntryDirection.In;

            var compEntry = new CashLedgerEntry
            {
                Id = Guid.NewGuid(),
                CashLedgerTransactionId = revTx.Id,
                BranchId = origEntry.BranchId,
                CashDrawerId = origEntry.CashDrawerId,
                ShiftId = origEntry.ShiftId,
                Direction = compDirection,
                Amount = origEntry.Amount,
                CurrencyCode = origEntry.CurrencyCode,
                CreatedAt = now,
                CreatedBy = userId
            };

            compensatingEntries.Add(compEntry);
            var shift = lockedShifts[origEntry.ShiftId];
            entryDtos.Add(new CashLedgerEntryDto(
                compEntry.Id,
                revTx.Id,
                compEntry.BranchId,
                compEntry.CashDrawerId,
                shift.CashDrawer?.Name,
                shift.CashDrawer?.DrawerCode,
                compEntry.ShiftId,
                shift.ShiftNumber,
                (int)compEntry.Direction,
                compDirection == CashLedgerEntryDirection.In ? "In" : "Out",
                compEntry.Amount,
                compEntry.CurrencyCode,
                compEntry.CreatedAt,
                userId,
                _currentUserService.Username
            ));
        }

        _context.CashLedgerTransactions.Add(revTx);
        _context.CashLedgerEntries.AddRange(compensatingEntries);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "CashLedgerTransaction",
            EntityId = revTx.Id.ToString(),
            Action = "CASH_TRANSACTION_REVERSED",
            UserId = userId,
            Timestamp = now,
            NewValues = JsonSerializer.Serialize(new
            {
                OriginalTransactionId = orig.Id,
                OriginalTransactionReference = orig.TransactionReference,
                ReversalTransactionReference = revTx.TransactionReference,
                Amount = revTx.Amount,
                CurrencyCode = revTx.CurrencyCode,
                ReversalReason = request.ReversalReason.Trim(),
                ReversedEntriesCount = compensatingEntries.Count
            })
        };
        _context.AuditLogs.Add(auditLog);

        var resultDto = new CashLedgerTransactionDto(
            revTx.Id,
            revTx.TransactionReference,
            revTx.BranchId,
            orig.Branch?.Name,
            (int)revTx.TransactionType,
            "Reversal",
            (int)revTx.SourceType,
            "Manual",
            null,
            revTx.Amount,
            revTx.CurrencyCode,
            revTx.Description,
            revTx.Reason,
            revTx.CategoryId,
            orig.Category?.Name,
            orig.Category?.Code,
            (int)revTx.Status,
            "Posted",
            revTx.TransactionAt,
            orig.Id,
            orig.TransactionReference,
            revTx.CreatedAt,
            userId,
            _currentUserService.Username,
            entryDtos
        );

        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = normalizedKey,
            Endpoint = "/api/cash-ledger/transactions/reverse",
            RequestHash = requestHash,
            ResponseBody = JsonSerializer.Serialize(resultDto),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);

            var committedRecord = await _context.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

            if (committedRecord != null)
            {
                if (committedRecord.RequestHash != requestHash)
                {
                    throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
                }

                var cachedDto = JsonSerializer.Deserialize<CashLedgerTransactionDto>(committedRecord.ResponseBody);
                if (cachedDto != null)
                {
                    return cachedDto;
                }
            }

            throw;
        }

        _logger.LogInformation("Reversed Cash Ledger Transaction {OrigRef} via Compensating Transaction {RevRef} by User {UserId}. Reason: {Reason}",
            orig.TransactionReference, revTx.TransactionReference, userId, request.ReversalReason);

        return resultDto;
    }

    public async Task<ShiftCashLedgerSummaryDto> GetShiftSummaryAsync(Guid shiftId, CancellationToken cancellationToken = default)
    {
        return await _expectedBalanceService.GetShiftCashSummaryAsync(shiftId, cancellationToken);
    }

    public async Task<List<CashMovementCategoryDto>> GetCategoriesAsync(CashCategoryDirection? direction = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.CashMovementCategories
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (direction.HasValue)
        {
            query = query.Where(c => c.AllowedDirection == direction.Value || c.AllowedDirection == CashCategoryDirection.Both);
        }

        var categories = await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return categories.Select(c => new CashMovementCategoryDto(
            c.Id,
            c.Name,
            c.Code,
            (int)c.AllowedDirection,
            c.Description,
            c.IsActive,
            c.IsSystem,
            c.CreatedAt
        )).ToList();
    }

    public async Task<CashMovementCategoryDto> CreateCategoryAsync(CreateCashMovementCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Category name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new BusinessRuleException("Category code is required.");
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var codeExists = await _context.CashMovementCategories
            .AnyAsync(c => c.Code.ToUpper() == normalizedCode && !c.IsDeleted, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException($"Cash movement category with code '{normalizedCode}' already exists.");
        }

        var category = new CashMovementCategory
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = normalizedCode,
            AllowedDirection = (CashCategoryDirection)request.AllowedDirection,
            Description = request.Description?.Trim(),
            IsActive = true,
            IsSystem = false
        };

        _context.CashMovementCategories.Add(category);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "CashMovementCategory",
            EntityId = category.Id.ToString(),
            Action = "CASH_CATEGORY_CREATED",
            UserId = _currentUserService.UserId ?? Guid.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            NewValues = JsonSerializer.Serialize(new { category.Name, category.Code, category.AllowedDirection, category.Description })
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken);

        return new CashMovementCategoryDto(
            category.Id,
            category.Name,
            category.Code,
            (int)category.AllowedDirection,
            category.Description,
            category.IsActive,
            category.IsSystem,
            category.CreatedAt
        );
    }

    public async Task<CashMovementCategoryDto> UpdateCategoryAsync(Guid id, UpdateCashMovementCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Category name is required.");
        }

        var category = await _context.CashMovementCategories
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("CashMovementCategory", id);

        var oldValues = new { category.Name, category.AllowedDirection, category.Description };

        category.Name = request.Name.Trim();
        category.AllowedDirection = (CashCategoryDirection)request.AllowedDirection;
        category.Description = request.Description?.Trim();
        category.UpdatedAt = DateTimeOffset.UtcNow;
        category.UpdatedBy = _currentUserService.UserId;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "CashMovementCategory",
            EntityId = category.Id.ToString(),
            Action = "CASH_CATEGORY_UPDATED",
            UserId = _currentUserService.UserId ?? Guid.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(new { category.Name, category.AllowedDirection, category.Description })
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken);

        return new CashMovementCategoryDto(
            category.Id,
            category.Name,
            category.Code,
            (int)category.AllowedDirection,
            category.Description,
            category.IsActive,
            category.IsSystem,
            category.CreatedAt
        );
    }

    public async Task<CashMovementCategoryDto> UpdateCategoryStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var category = await _context.CashMovementCategories
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("CashMovementCategory", id);

        if (category.IsActive == isActive)
        {
            return new CashMovementCategoryDto(
                category.Id, category.Name, category.Code, (int)category.AllowedDirection,
                category.Description, category.IsActive, category.IsSystem, category.CreatedAt);
        }

        var oldActive = category.IsActive;
        category.IsActive = isActive;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        category.UpdatedBy = _currentUserService.UserId;

        var action = isActive ? "CASH_CATEGORY_ACTIVATED" : "CASH_CATEGORY_DEACTIVATED";
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "CashMovementCategory",
            EntityId = category.Id.ToString(),
            Action = action,
            UserId = _currentUserService.UserId ?? Guid.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            OldValues = JsonSerializer.Serialize(new { IsActive = oldActive }),
            NewValues = JsonSerializer.Serialize(new { IsActive = isActive })
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken);

        return new CashMovementCategoryDto(
            category.Id,
            category.Name,
            category.Code,
            (int)category.AllowedDirection,
            category.Description,
            category.IsActive,
            category.IsSystem,
            category.CreatedAt
        );
    }

    public async Task<CashLedgerTransactionDto> PostCashPaymentAsync(CashLedgerSourceType sourceType, string sourceReferenceId, Guid shiftId, decimal amount, string currencyCode, string? notes, bool autoSave = true, CancellationToken cancellationToken = default)
    {
        ValidateMonetaryAmount(amount);

        if (string.IsNullOrWhiteSpace(sourceReferenceId))
        {
            throw new BusinessRuleException("Source reference ID is required for automated POS cash payment postings.");
        }

        // Idempotency / duplicate check for POS payments (Section 33)
        var exists = await _context.CashLedgerTransactions
            .AnyAsync(t => t.SourceType == sourceType && t.SourceReferenceId == sourceReferenceId, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"A cash ledger entry for {sourceType} with reference '{sourceReferenceId}' has already been posted.");
        }

        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Shifts\" WHERE \"Id\" = {shiftId} FOR UPDATE", cancellationToken);
        var shift = await _context.Shifts
            .Include(s => s.CashDrawer)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new NotFoundException("Shift", shiftId);

        ValidateShiftForPosting(shift, isSystem: true);

        var txRef = await _tagGenerator.GenerateCashLedgerReferenceAsync(shift.Branch?.Code ?? "BR", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var userId = _currentUserService.UserId ?? shift.CashierId;

        var tx = new CashLedgerTransaction
        {
            Id = Guid.NewGuid(),
            TransactionReference = txRef,
            BranchId = shift.BranchId,
            TransactionType = CashLedgerTransactionType.CashIn,
            SourceType = sourceType,
            SourceReferenceId = sourceReferenceId.Trim(),
            Amount = amount,
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? shift.CurrencyCode : currencyCode.Trim().ToUpperInvariant(),
            Description = notes?.Trim() ?? $"Automated POS payment received ({sourceType})",
            Status = CashLedgerStatus.Posted,
            TransactionAt = now,
            CreatedAt = now,
            CreatedBy = userId
        };

        var entry = new CashLedgerEntry
        {
            Id = Guid.NewGuid(),
            CashLedgerTransactionId = tx.Id,
            BranchId = shift.BranchId,
            CashDrawerId = shift.CashDrawerId,
            ShiftId = shift.Id,
            Direction = CashLedgerEntryDirection.In,
            Amount = amount,
            CurrencyCode = tx.CurrencyCode,
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.CashLedgerTransactions.Add(tx);
        _context.CashLedgerEntries.Add(entry);

        if (autoSave)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return MapToTransactionDto(tx);
    }

    public async Task<CashLedgerTransactionDto> PostCashRefundAsync(CashLedgerSourceType sourceType, string sourceReferenceId, Guid shiftId, decimal amount, string currencyCode, string? notes, bool autoSave = true, CancellationToken cancellationToken = default)
    {
        ValidateMonetaryAmount(amount);

        if (string.IsNullOrWhiteSpace(sourceReferenceId))
        {
            throw new BusinessRuleException("Source reference ID is required for automated POS cash refund postings.");
        }

        var exists = await _context.CashLedgerTransactions
            .AnyAsync(t => t.SourceType == sourceType && t.SourceReferenceId == sourceReferenceId, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"A cash ledger entry for {sourceType} with reference '{sourceReferenceId}' has already been posted.");
        }

        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Shifts\" WHERE \"Id\" = {shiftId} FOR UPDATE", cancellationToken);
        var shift = await _context.Shifts
            .Include(s => s.CashDrawer)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new NotFoundException("Shift", shiftId);

        ValidateShiftForPosting(shift, isSystem: true);

        var currentExpected = await _expectedBalanceService.CalculateExpectedClosingBalanceAsync(shift.Id, shift.OpeningBalance, cancellationToken);
        if (currentExpected < amount)
        {
            throw new BusinessRuleException($"Insufficient cash in drawer '{shift.CashDrawer?.Name}' to process cash refund of {shift.CurrencyCode} {amount:N2}. Current balance is {currentExpected:N2}.");
        }

        var txRef = await _tagGenerator.GenerateCashLedgerReferenceAsync(shift.Branch?.Code ?? "BR", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var userId = _currentUserService.UserId ?? shift.CashierId;

        var tx = new CashLedgerTransaction
        {
            Id = Guid.NewGuid(),
            TransactionReference = txRef,
            BranchId = shift.BranchId,
            TransactionType = CashLedgerTransactionType.CashOut,
            SourceType = sourceType,
            SourceReferenceId = sourceReferenceId.Trim(),
            Amount = amount,
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? shift.CurrencyCode : currencyCode.Trim().ToUpperInvariant(),
            Description = notes?.Trim() ?? $"Automated POS refund issued ({sourceType})",
            Status = CashLedgerStatus.Posted,
            TransactionAt = now,
            CreatedAt = now,
            CreatedBy = userId
        };

        var entry = new CashLedgerEntry
        {
            Id = Guid.NewGuid(),
            CashLedgerTransactionId = tx.Id,
            BranchId = shift.BranchId,
            CashDrawerId = shift.CashDrawerId,
            ShiftId = shift.Id,
            Direction = CashLedgerEntryDirection.Out,
            Amount = amount,
            CurrencyCode = tx.CurrencyCode,
            CreatedAt = now,
            CreatedBy = userId
        };

        _context.CashLedgerTransactions.Add(tx);
        _context.CashLedgerEntries.Add(entry);

        if (autoSave)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return MapToTransactionDto(tx);
    }

    private static void ValidateMonetaryAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new BusinessRuleException("Transaction amount must be strictly greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new BusinessRuleException("Transaction amount cannot exceed 2 decimal places.");
        }
    }

    private void ValidateShiftForPosting(Shift shift, bool isSystem = false, bool isTransfer = false)
    {
        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException($"Shift '{shift.ShiftNumber}' is closed and cannot accept new physical cash movements.");
        }

        if (shift.CashDrawer == null || !shift.CashDrawer.IsActive)
        {
            throw new BusinessRuleException($"Cash drawer '{shift.CashDrawer?.Name ?? shift.CashDrawerId.ToString()}' is inactive or invalid.");
        }

        if (isSystem)
        {
            return;
        }

        // Branch check
        if (!_currentUserService.IsGlobalAdmin && !_currentUserService.AccessibleBranchIds.Contains(shift.BranchId))
        {
            throw new ForbiddenException("You are not authorized to post transactions to this branch.");
        }

        // Cashier ownership and granular financial permissions:
        // SHIFT_OVERRIDE_CLOSE must NOT implicitly authorize financial postings, transfers, or reversals.
        if (isTransfer)
        {
            var isTransferAuthorized = _currentUserService.IsGlobalAdmin
                || _currentUserService.Permissions.Contains(Permissions.CashLedgerTransfer, StringComparer.OrdinalIgnoreCase);

            if (!isTransferAuthorized)
            {
                throw new ForbiddenException("You do not possess the required CASH_LEDGER_TRANSFER permission.");
            }
        }
        else
        {
            // For Cash In / Cash Out: only the cashier assigned to the shift (or global admin) can post to it.
            if (!_currentUserService.IsGlobalAdmin && shift.CashierId != _currentUserService.UserId)
            {
                throw new BusinessRuleException("Cashiers may only post physical cash movements to their own active open shift.");
            }
        }
    }

    private static CashLedgerTransactionDto MapToTransactionDto(CashLedgerTransaction t)
    {
        var typeName = t.TransactionType switch
        {
            CashLedgerTransactionType.CashIn => "Cash In",
            CashLedgerTransactionType.CashOut => "Cash Out",
            CashLedgerTransactionType.Transfer => "Transfer",
            CashLedgerTransactionType.Reversal => "Reversal",
            _ => t.TransactionType.ToString()
        };

        var sourceName = t.SourceType switch
        {
            CashLedgerSourceType.Manual => "Manual",
            CashLedgerSourceType.PosSale => "POS Sale",
            CashLedgerSourceType.PosRefund => "POS Refund",
            CashLedgerSourceType.RepairPayment => "Repair Payment",
            CashLedgerSourceType.OrderPayment => "Order Payment",
            CashLedgerSourceType.OldGoldPayment => "Old Gold Payment",
            CashLedgerSourceType.SystemReversal => "System Reversal",
            _ => t.SourceType.ToString()
        };

        var statusName = t.Status switch
        {
            CashLedgerStatus.Posted => "Posted",
            CashLedgerStatus.Reversed => "Reversed",
            _ => t.Status.ToString()
        };

        var entries = t.Entries?.Select(e => new CashLedgerEntryDto(
            e.Id,
            e.CashLedgerTransactionId,
            e.BranchId,
            e.CashDrawerId,
            e.CashDrawer?.Name,
            e.CashDrawer?.DrawerCode,
            e.ShiftId,
            e.Shift?.ShiftNumber,
            (int)e.Direction,
            e.Direction == CashLedgerEntryDirection.In ? "In" : "Out",
            e.Amount,
            e.CurrencyCode,
            e.CreatedAt,
            e.CreatedBy,
            e.Creator?.FullName ?? e.Creator?.Email
        )).ToList() ?? new List<CashLedgerEntryDto>();

        return new CashLedgerTransactionDto(
            t.Id,
            t.TransactionReference,
            t.BranchId,
            t.Branch?.Name,
            (int)t.TransactionType,
            typeName,
            (int)t.SourceType,
            sourceName,
            t.SourceReferenceId,
            t.Amount,
            t.CurrencyCode,
            t.Description,
            t.Reason,
            t.CategoryId,
            t.CategoryNameSnapshot ?? t.Category?.Name,
            t.CategoryCodeSnapshot ?? t.Category?.Code,
            (int)t.Status,
            statusName,
            t.TransactionAt,
            t.ReversalOfTransactionId,
            t.ReversalOfTransaction?.TransactionReference,
            t.CreatedAt,
            t.CreatedBy,
            t.Creator?.FullName ?? t.Creator?.Email,
            entries
        );
    }
}

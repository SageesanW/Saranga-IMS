using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Shifts;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public class ShiftService : IShiftService
{
    private readonly IAppDbContext _context;
    private readonly ITagGeneratorService _tagGeneratorService;
    private readonly IShiftExpectedBalanceService _expectedBalanceService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ShiftService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ShiftService(
        IAppDbContext context,
        ITagGeneratorService tagGeneratorService,
        IShiftExpectedBalanceService expectedBalanceService,
        ICurrentUserService currentUserService,
        ILogger<ShiftService> logger)
    {
        _context = context;
        _tagGeneratorService = tagGeneratorService;
        _expectedBalanceService = expectedBalanceService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken cancellationToken = default)
    {
        var cashierId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Authenticated user identity is required to open a shift.");

        if (request.OpeningBalance < 0)
        {
            throw new BusinessRuleException("Opening cash float balance cannot be negative.");
        }

        // Validate Branch
        var branch = await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        if (!branch.IsActive)
        {
            throw new BusinessRuleException($"Branch '{branch.Name}' is inactive.");
        }

        // Validate Cash Drawer
        var drawer = await _context.CashDrawers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.CashDrawerId && d.BranchId == request.BranchId, cancellationToken)
            ?? throw new NotFoundException($"Cash drawer was not found in branch '{branch.Name}'.");

        if (!drawer.IsActive)
        {
            throw new BusinessRuleException($"Cash drawer '{drawer.Name}' is inactive.");
        }

        // Rule: One active shift per cash drawer
        var activeDrawerShift = await _context.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CashDrawerId == request.CashDrawerId && s.Status == ShiftStatus.Open, cancellationToken);

        if (activeDrawerShift != null)
        {
            throw new BusinessRuleException($"Cash drawer '{drawer.Name}' already has an active open shift ({activeDrawerShift.ShiftNumber}).");
        }

        // Rule: One active shift per cashier for MVP
        var activeCashierShift = await _context.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == ShiftStatus.Open, cancellationToken);

        if (activeCashierShift != null)
        {
            throw new BusinessRuleException($"Cashier already has an active open shift ({activeCashierShift.ShiftNumber}). Please close the current active shift before opening a new one.");
        }

        // Rule: Opening balance defaults to previous shift's actual closing balance
        var lastClosedShift = await _context.Shifts
            .AsNoTracking()
            .Where(s => s.CashDrawerId == request.CashDrawerId && s.Status == ShiftStatus.Closed)
            .OrderByDescending(s => s.ClosedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var systemExpectedOpening = lastClosedShift?.ActualClosingBalance ?? 0.00m;
        var openingDifference = request.OpeningBalance - systemExpectedOpening;

        // Rule: Any opening balance difference requires a mandatory reason
        if (openingDifference != 0m && string.IsNullOrWhiteSpace(request.OpeningDifferenceReason))
        {
            throw new BusinessRuleException($"An opening balance discrepancy of Rs. {Math.Abs(openingDifference):N2} (System expected: Rs. {systemExpectedOpening:N2}, Entered: Rs. {request.OpeningBalance:N2}) requires a mandatory reason.");
        }

        // Sequential Shift Number from PostgreSQL sequence
        var shiftNumber = await _tagGeneratorService.GenerateShiftNumberAsync(branch.Code, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var shift = new Shift
        {
            ShiftNumber = shiftNumber,
            BranchId = request.BranchId,
            CashDrawerId = request.CashDrawerId,
            CashierId = cashierId,
            Status = ShiftStatus.Open,
            OpenedAt = now,
            OpenedBy = cashierId,
            OpeningBalance = request.OpeningBalance,
            SystemExpectedOpeningBalance = systemExpectedOpening,
            OpeningBalanceDifference = openingDifference,
            OpeningDifferenceReason = openingDifference != 0m ? request.OpeningDifferenceReason?.Trim() : null,
            OpeningNotes = string.IsNullOrWhiteSpace(request.OpeningNotes) ? null : request.OpeningNotes.Trim(),
            CreatedAt = now,
            CreatedBy = cashierId
        };

        _context.Shifts.Add(shift);

        RecordAudit("Shift", shift.Id, "SHIFT_OPENED", null, new
        {
            shift.ShiftNumber,
            shift.BranchId,
            shift.CashDrawerId,
            shift.CashierId,
            shift.OpeningBalance,
            shift.SystemExpectedOpeningBalance,
            shift.OpeningBalanceDifference,
            shift.OpeningDifferenceReason
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("CashDrawerId") || msg.Contains("IX_Shifts_CashDrawerId"))
            {
                throw new BusinessRuleException($"Cash drawer '{drawer.Name}' already has an active open shift.");
            }
            if (msg.Contains("CashierId") || msg.Contains("IX_Shifts_CashierId"))
            {
                throw new BusinessRuleException("Cashier already has an active open shift in progress.");
            }
            if (msg.Contains("23505"))
            {
                throw new BusinessRuleException("A concurrent shift conflict occurred. Please refresh.");
            }
            throw;
        }

        _logger.LogInformation("Shift {ShiftNumber} successfully opened for Cashier {CashierId} at Drawer {DrawerName} with opening balance {OpeningBalance} LKR",
            shift.ShiftNumber, cashierId, drawer.Name, shift.OpeningBalance);

        return await GetShiftByIdAsync(shift.Id, cancellationToken);
    }

    public async Task<ShiftDto> CloseShiftAsync(Guid shiftId, CloseShiftRequest request, CancellationToken cancellationToken = default)
    {
        var closerId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Authenticated user identity is required to close a shift.");

        if (request.ActualClosingBalance < 0)
        {
            throw new BusinessRuleException("Actual physical closing cash balance cannot be negative.");
        }

        var shift = await _context.Shifts
            .Include(s => s.CashDrawer)
            .Include(s => s.Branch)
            .Include(s => s.Cashier)
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new NotFoundException("Shift", shiftId);

        // Immutability: Closed shifts can never be modified or re-closed
        if (shift.Status == ShiftStatus.Closed)
        {
            throw new BusinessRuleException($"Shift '{shift.ShiftNumber}' was already closed at {shift.ClosedAt:yyyy-MM-dd HH:mm} and is immutable.");
        }

        // Optimistic concurrency protection
        if (request.RowVersion.HasValue && shift.Version != request.RowVersion.Value)
        {
            throw new ConflictException("This shift was modified concurrently by another user. Please refresh and review latest shift details.");
        }

        // Rule: Expected closing balance must be calculated by the backend
        var expectedClosing = await _expectedBalanceService.CalculateExpectedClosingBalanceAsync(shift.Id, shift.OpeningBalance, cancellationToken);

        // Rule: Closing variance equals actual physical cash minus expected cash
        var variance = request.ActualClosingBalance - expectedClosing;

        // Rule: Any non-zero closing variance requires a mandatory reason
        if (variance != 0m && string.IsNullOrWhiteSpace(request.ClosingVarianceReason))
        {
            var varianceType = variance < 0 ? "Shortage" : "Surplus";
            throw new BusinessRuleException($"A closing cash variance of Rs. {Math.Abs(variance):N2} ({varianceType}) requires a mandatory explanation reason.");
        }

        var oldState = new
        {
            shift.Status,
            shift.OpeningBalance
        };

        var now = DateTimeOffset.UtcNow;
        shift.ActualClosingBalance = request.ActualClosingBalance;
        shift.ExpectedClosingBalance = expectedClosing;
        shift.ClosingVariance = variance;
        shift.ClosingVarianceReason = variance != 0m ? request.ClosingVarianceReason?.Trim() : null;
        shift.ClosingNotes = string.IsNullOrWhiteSpace(request.ClosingNotes) ? null : request.ClosingNotes.Trim();
        shift.Status = ShiftStatus.Closed;
        shift.ClosedAt = now;
        shift.ClosedBy = closerId;
        shift.UpdatedAt = now;
        shift.UpdatedBy = closerId;

        // Audit logging: never generate artificial cash movements to conceal discrepancies
        RecordAudit("Shift", shift.Id, "SHIFT_CLOSED", oldState, new
        {
            shift.ShiftNumber,
            shift.Status,
            shift.OpeningBalance,
            shift.ActualClosingBalance,
            shift.ExpectedClosingBalance,
            shift.ClosingVariance,
            shift.ClosingVarianceReason,
            shift.ClosedAt,
            shift.ClosedBy
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Shift {ShiftNumber} successfully closed by User {CloserId}. Actual: {Actual:N2}, Expected: {Expected:N2}, Variance: {Variance:N2} LKR",
            shift.ShiftNumber, closerId, shift.ActualClosingBalance, shift.ExpectedClosingBalance, shift.ClosingVariance);

        return await GetShiftByIdAsync(shift.Id, cancellationToken);
    }

    public async Task<ShiftDto> GetShiftByIdAsync(Guid shiftId, CancellationToken cancellationToken = default)
    {
        var shift = await _context.Shifts
            .AsNoTracking()
            .Include(s => s.Branch)
            .Include(s => s.CashDrawer)
            .Include(s => s.Cashier)
            .Include(s => s.Opener)
            .Include(s => s.Closer)
            .FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new NotFoundException("Shift", shiftId);

        return MapToDto(shift);
    }

    public async Task<ShiftDto?> GetActiveShiftForCashierAsync(Guid? cashierId = null, CancellationToken cancellationToken = default)
    {
        var targetCashierId = cashierId ?? _currentUserService.UserId;
        if (!targetCashierId.HasValue)
            return null;

        var shift = await _context.Shifts
            .AsNoTracking()
            .Include(s => s.Branch)
            .Include(s => s.CashDrawer)
            .Include(s => s.Cashier)
            .Include(s => s.Opener)
            .Include(s => s.Closer)
            .FirstOrDefaultAsync(s => s.CashierId == targetCashierId.Value && s.Status == ShiftStatus.Open, cancellationToken);

        return shift != null ? MapToDto(shift) : null;
    }

    public async Task<ShiftDto?> GetActiveShiftForDrawerAsync(Guid drawerId, CancellationToken cancellationToken = default)
    {
        var shift = await _context.Shifts
            .AsNoTracking()
            .Include(s => s.Branch)
            .Include(s => s.CashDrawer)
            .Include(s => s.Cashier)
            .Include(s => s.Opener)
            .Include(s => s.Closer)
            .FirstOrDefaultAsync(s => s.CashDrawerId == drawerId && s.Status == ShiftStatus.Open, cancellationToken);

        return shift != null ? MapToDto(shift) : null;
    }

    public async Task<ShiftOpeningDefaultsDto> GetOpeningDefaultsAsync(Guid drawerId, CancellationToken cancellationToken = default)
    {
        var drawer = await _context.CashDrawers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == drawerId, cancellationToken)
            ?? throw new NotFoundException("Cash Drawer", drawerId);

        var lastClosedShift = await _context.Shifts
            .AsNoTracking()
            .Where(s => s.CashDrawerId == drawerId && s.Status == ShiftStatus.Closed)
            .OrderByDescending(s => s.ClosedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var expectedOpening = lastClosedShift?.ActualClosingBalance ?? 0.00m;

        return new ShiftOpeningDefaultsDto(
            CashDrawerId: drawer.Id,
            DrawerName: drawer.Name,
            SystemExpectedOpeningBalance: expectedOpening,
            PreviousShiftClosedAt: lastClosedShift?.ClosedAt,
            PreviousShiftNumber: lastClosedShift?.ShiftNumber
        );
    }

    public async Task<PagedResult<ShiftDto>> GetShiftsAsync(ShiftQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var query = _context.Shifts
            .AsNoTracking()
            .Include(s => s.Branch)
            .Include(s => s.CashDrawer)
            .Include(s => s.Cashier)
            .Include(s => s.Opener)
            .Include(s => s.Closer)
            .AsQueryable();

        if (queryParams.BranchId.HasValue)
        {
            query = query.Where(s => s.BranchId == queryParams.BranchId.Value);
        }

        if (queryParams.CashDrawerId.HasValue)
        {
            query = query.Where(s => s.CashDrawerId == queryParams.CashDrawerId.Value);
        }

        if (queryParams.CashierId.HasValue)
        {
            query = query.Where(s => s.CashierId == queryParams.CashierId.Value);
        }

        if (queryParams.Status.HasValue)
        {
            query = query.Where(s => s.Status == queryParams.Status.Value);
        }

        if (queryParams.StartDate.HasValue)
        {
            query = query.Where(s => s.OpenedAt >= queryParams.StartDate.Value);
        }

        if (queryParams.EndDate.HasValue)
        {
            query = query.Where(s => s.OpenedAt <= queryParams.EndDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = queryParams.Page < 1 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize is < 1 or > 200 ? 25 : queryParams.PageSize;

        var items = await query
            .OrderByDescending(s => s.OpenedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<ShiftDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<List<CashDrawerDto>> GetCashDrawersAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var drawers = await _context.CashDrawers
            .AsNoTracking()
            .Where(d => d.BranchId == branchId && d.IsActive)
            .OrderBy(d => d.DrawerCode)
            .ToListAsync(cancellationToken);

        // Fetch active shifts for this branch to mark drawer status
        var activeShifts = await _context.Shifts
            .AsNoTracking()
            .Include(s => s.Cashier)
            .Where(s => s.BranchId == branchId && s.Status == ShiftStatus.Open)
            .ToListAsync(cancellationToken);

        var activeShiftByDrawer = activeShifts.ToDictionary(s => s.CashDrawerId);

        return drawers.Select(d =>
        {
            var hasActive = activeShiftByDrawer.TryGetValue(d.Id, out var shift);
            return new CashDrawerDto(
                d.Id,
                d.BranchId,
                d.DrawerCode,
                d.Name,
                d.Description,
                d.IsActive,
                HasActiveShift: hasActive,
                ActiveShiftId: shift?.Id,
                ActiveCashierName: shift?.Cashier?.FullName
            );
        }).ToList();
    }

    public async Task<CashDrawerDto> CreateCashDrawerAsync(CreateCashDrawerRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DrawerCode))
        {
            throw new BusinessRuleException("Drawer code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Drawer name is required.");
        }

        var branch = await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        var cleanCode = request.DrawerCode.Trim().ToUpperInvariant();
        var exists = await _context.CashDrawers
            .AnyAsync(d => d.BranchId == request.BranchId && d.DrawerCode == cleanCode, cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException($"A cash drawer with code '{cleanCode}' already exists in branch '{branch.Name}'.");
        }

        var drawer = new CashDrawer
        {
            BranchId = request.BranchId,
            DrawerCode = cleanCode,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.CashDrawers.Add(drawer);
        await _context.SaveChangesAsync(cancellationToken);

        return new CashDrawerDto(
            drawer.Id,
            drawer.BranchId,
            drawer.DrawerCode,
            drawer.Name,
            drawer.Description,
            drawer.IsActive
        );
    }

    private static ShiftDto MapToDto(Shift s)
    {
        return new ShiftDto(
            s.Id,
            s.ShiftNumber,
            s.BranchId,
            s.Branch?.Name,
            s.CashDrawerId,
            s.CashDrawer?.Name,
            s.CashDrawer?.DrawerCode,
            s.CashierId,
            s.Cashier?.FullName,
            s.Status,
            s.OpenedAt,
            s.OpenedBy,
            s.Opener?.FullName,
            s.OpeningBalance,
            s.SystemExpectedOpeningBalance,
            s.OpeningBalanceDifference,
            s.OpeningDifferenceReason,
            s.OpeningNotes,
            s.ClosedAt,
            s.ClosedBy,
            s.Closer?.FullName,
            s.ActualClosingBalance,
            s.ExpectedClosingBalance,
            s.ClosingVariance,
            s.ClosingVarianceReason,
            s.ClosingNotes,
            s.Version
        );
    }

    private void RecordAudit(string entityName, Guid entityId, string action, object? oldValues, object? newValues)
    {
        var audit = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Action = action,
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = _currentUserService.BranchId,
            Timestamp = DateTimeOffset.UtcNow,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent
        };

        _context.AuditLogs.Add(audit);
    }
}

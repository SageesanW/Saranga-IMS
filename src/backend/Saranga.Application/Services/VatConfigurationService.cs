using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Rates;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;

namespace Saranga.Application.Services;

public class VatConfigurationService : IVatConfigurationService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchAuthorizationService _branchAuthService;
    private readonly ILogger<VatConfigurationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public VatConfigurationService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IBranchAuthorizationService branchAuthService,
        ILogger<VatConfigurationService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _branchAuthService = branchAuthService;
        _logger = logger;
    }

    public async Task<VatConfigurationDto> GetActiveVatConfigurationAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(branchId, cancellationToken);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);

        var config = await _context.VatConfigurations
            .AsNoTracking()
            .Include(v => v.Branch)
            .Include(v => v.Creator)
            .Include(v => v.Updater)
            .FirstOrDefaultAsync(v => v.BranchId == branchId && v.IsActive, cancellationToken);

        if (config == null)
        {
            // Return virtual default inactive configuration if none configured yet
            return new VatConfigurationDto(
                Id: Guid.Empty,
                BranchId: branch.Id,
                BranchCode: branch.Code,
                BranchName: branch.Name,
                Name: "Standard VAT (Unconfigured)",
                IsEnabled: false,
                RatePercentage: 0.00m,
                EffectiveFrom: DateTimeOffset.UtcNow,
                EffectiveTo: null,
                CalculationMode: VatCalculationModes.FlatPercentage,
                IsActive: true,
                CreatedAt: DateTimeOffset.UtcNow,
                CreatedBy: null,
                CreatedByName: null,
                UpdatedAt: null,
                UpdatedBy: null,
                UpdatedByName: null
            );
        }

        return MapToDto(config);
    }

    public async Task<PagedResult<VatConfigurationDto>> GetVatHistoryAsync(VatHistoryQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        if (queryParams.BranchId.HasValue && queryParams.BranchId != Guid.Empty)
        {
            await _branchAuthService.EnsureBranchAccessAsync(queryParams.BranchId.Value, cancellationToken);
        }
        else if (!_currentUserService.IsGlobalAdmin && _currentUserService.BranchId.HasValue)
        {
            queryParams = queryParams with { BranchId = _currentUserService.BranchId };
        }

        var query = _context.VatConfigurations
            .AsNoTracking()
            .Include(v => v.Branch)
            .Include(v => v.Creator)
            .Include(v => v.Updater)
            .AsQueryable();

        if (queryParams.BranchId.HasValue && queryParams.BranchId != Guid.Empty)
        {
            query = query.Where(v => v.BranchId == queryParams.BranchId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = queryParams.Page < 1 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize is < 1 or > 200 ? 50 : queryParams.PageSize;

        var items = await query
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<VatConfigurationDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<VatConfigurationDto> CreateOrUpdateVatConfigurationAsync(CreateVatConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        if (request.RatePercentage < 0m || request.RatePercentage > 100m)
        {
            throw new BusinessRuleException("VAT rate percentage must be between 0.00% and 100.00%.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        if (!branch.IsActive)
        {
            throw new BusinessRuleException($"Branch '{branch.Name}' is inactive.");
        }

        var now = DateTimeOffset.UtcNow;
        var effectiveFrom = request.EffectiveFrom ?? now;

        // Deactivate existing active configuration
        var existingActive = await _context.VatConfigurations
            .Where(v => v.BranchId == request.BranchId && v.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var v in existingActive)
        {
            v.IsActive = false;
            v.EffectiveTo = effectiveFrom;
            v.UpdatedAt = now;
            v.UpdatedBy = _currentUserService.UserId;
        }

        var newConfig = new VatConfiguration
        {
            BranchId = request.BranchId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Standard VAT" : request.Name.Trim(),
            IsEnabled = request.IsEnabled,
            RatePercentage = Math.Round(request.RatePercentage, 2),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null,
            CalculationMode = string.IsNullOrWhiteSpace(request.CalculationMode) ? VatCalculationModes.FlatPercentage : request.CalculationMode.Trim(),
            IsActive = true,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserId
        };

        _context.VatConfigurations.Add(newConfig);

        RecordAudit("VatConfiguration", newConfig.Id, "VAT_CONFIG_UPDATED", null, new
        {
            BranchCode = branch.Code,
            newConfig.Name,
            newConfig.IsEnabled,
            newConfig.RatePercentage,
            newConfig.EffectiveFrom,
            newConfig.CalculationMode
        }, branch.Id);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetVatConfigByIdAsync(newConfig.Id, cancellationToken);
    }

    public async Task<VatConfigurationDto> ToggleVatStatusAsync(ToggleVatStatusRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        var now = DateTimeOffset.UtcNow;

        var existingConfig = await _context.VatConfigurations
            .FirstOrDefaultAsync(v => v.BranchId == request.BranchId && v.IsActive, cancellationToken);

        if (existingConfig == null)
        {
            // Create initial configuration
            return await CreateOrUpdateVatConfigurationAsync(new CreateVatConfigurationRequest(
                BranchId: request.BranchId,
                Name: "Standard VAT",
                IsEnabled: request.IsEnabled,
                RatePercentage: 18.00m // standard Sri Lankan rate default
            ), cancellationToken);
        }

        // Deactivate old config and create new version
        existingConfig.IsActive = false;
        existingConfig.EffectiveTo = now;
        existingConfig.UpdatedAt = now;
        existingConfig.UpdatedBy = _currentUserService.UserId;

        var newConfig = new VatConfiguration
        {
            BranchId = request.BranchId,
            Name = existingConfig.Name,
            IsEnabled = request.IsEnabled,
            RatePercentage = existingConfig.RatePercentage,
            EffectiveFrom = now,
            EffectiveTo = null,
            CalculationMode = existingConfig.CalculationMode,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserId
        };

        _context.VatConfigurations.Add(newConfig);

        RecordAudit("VatConfiguration", newConfig.Id, "VAT_STATUS_TOGGLED",
            new { IsEnabled = existingConfig.IsEnabled },
            new { IsEnabled = newConfig.IsEnabled, Reason = request.Reason },
            branch.Id);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetVatConfigByIdAsync(newConfig.Id, cancellationToken);
    }

    private async Task<VatConfigurationDto> GetVatConfigByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var config = await _context.VatConfigurations
            .AsNoTracking()
            .Include(v => v.Branch)
            .Include(v => v.Creator)
            .Include(v => v.Updater)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw new NotFoundException("VatConfiguration", id);

        return MapToDto(config);
    }

    private static VatConfigurationDto MapToDto(VatConfiguration v)
    {
        return new VatConfigurationDto(
            v.Id,
            v.BranchId,
            v.Branch?.Code ?? string.Empty,
            v.Branch?.Name ?? string.Empty,
            v.Name,
            v.IsEnabled,
            v.RatePercentage,
            v.EffectiveFrom,
            v.EffectiveTo,
            v.CalculationMode,
            v.IsActive,
            v.CreatedAt,
            v.CreatedBy,
            v.Creator?.FullName,
            v.UpdatedAt,
            v.UpdatedBy,
            v.Updater?.FullName
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
}

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

public class MetalRateService : IMetalRateService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchAuthorizationService _branchAuthService;
    private readonly IMetalRateProvider _metalRateProvider;
    private readonly ILogger<MetalRateService> _logger;

    // In-memory telemetry for external rate provider status
    private static DateTimeOffset? _lastSuccessfulRefreshAt;
    private static DateTimeOffset? _lastAttemptAt;
    private static string? _lastErrorMessage;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MetalRateService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IBranchAuthorizationService branchAuthService,
        IMetalRateProvider metalRateProvider,
        ILogger<MetalRateService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _branchAuthService = branchAuthService;
        _metalRateProvider = metalRateProvider;
        _logger = logger;
    }

    public async Task<List<MetalRateDto>> GetActiveRatesAsync(Guid? branchId = null, string? currencyCode = null, CancellationToken cancellationToken = default)
    {
        var targetBranchId = branchId ?? _currentUserService.BranchId;
        if (!targetBranchId.HasValue || targetBranchId == Guid.Empty)
        {
            if (_currentUserService.IsGlobalAdmin)
            {
                var firstBranch = await _context.Branches.FirstOrDefaultAsync(b => b.IsActive, cancellationToken);
                if (firstBranch != null) targetBranchId = firstBranch.Id;
            }
        }

        if (targetBranchId.HasValue && targetBranchId != Guid.Empty)
        {
            await _branchAuthService.EnsureBranchAccessAsync(targetBranchId.Value, cancellationToken);
        }

        var query = _context.MetalRates
            .AsNoTracking()
            .Include(r => r.Branch)
            .Include(r => r.Material)
            .Include(r => r.Purity)
            .Include(r => r.Creator)
            .Where(r => r.IsActive);

        if (targetBranchId.HasValue && targetBranchId != Guid.Empty)
        {
            query = query.Where(r => r.BranchId == targetBranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            query = query.Where(r => r.CurrencyCode == currencyCode.Trim().ToUpperInvariant());
        }

        var rates = await query
            .OrderBy(r => r.Material.SortOrder)
            .ThenByDescending(r => r.Purity.KaratValue)
            .ToListAsync(cancellationToken);

        return rates.Select(MapToDto).ToList();
    }

    public async Task<MetalRateDto> GetActiveRateAsync(Guid branchId, Guid materialId, Guid purityId, string? currencyCode = null, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(branchId, cancellationToken);

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);

        var targetCurrency = string.IsNullOrWhiteSpace(currencyCode) ? branch.CurrencyCode : currencyCode.Trim().ToUpperInvariant();

        var rate = await _context.MetalRates
            .AsNoTracking()
            .Include(r => r.Branch)
            .Include(r => r.Material)
            .Include(r => r.Purity)
            .Include(r => r.Creator)
            .FirstOrDefaultAsync(r => r.BranchId == branchId
                                   && r.MaterialId == materialId
                                   && r.PurityId == purityId
                                   && r.CurrencyCode == targetCurrency
                                   && r.IsActive, cancellationToken);

        if (rate == null)
        {
            throw new NotFoundException($"Active metal rate for branch {branch.Code}, material {materialId}, purity {purityId}, currency {targetCurrency} was not found.");
        }

        return MapToDto(rate);
    }

    public async Task<PagedResult<MetalRateDto>> GetRateHistoryAsync(MetalRateHistoryQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        if (queryParams.BranchId.HasValue && queryParams.BranchId != Guid.Empty)
        {
            await _branchAuthService.EnsureBranchAccessAsync(queryParams.BranchId.Value, cancellationToken);
        }
        else if (!_currentUserService.IsGlobalAdmin && _currentUserService.BranchId.HasValue)
        {
            queryParams = queryParams with { BranchId = _currentUserService.BranchId };
        }

        var query = _context.MetalRates
            .AsNoTracking()
            .Include(r => r.Branch)
            .Include(r => r.Material)
            .Include(r => r.Purity)
            .Include(r => r.Creator)
            .AsQueryable();

        if (queryParams.BranchId.HasValue && queryParams.BranchId != Guid.Empty)
        {
            query = query.Where(r => r.BranchId == queryParams.BranchId.Value);
        }

        if (queryParams.MaterialId.HasValue && queryParams.MaterialId != Guid.Empty)
        {
            query = query.Where(r => r.MaterialId == queryParams.MaterialId.Value);
        }

        if (queryParams.PurityId.HasValue && queryParams.PurityId != Guid.Empty)
        {
            query = query.Where(r => r.PurityId == queryParams.PurityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.CurrencyCode))
        {
            query = query.Where(r => r.CurrencyCode == queryParams.CurrencyCode.Trim().ToUpperInvariant());
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = queryParams.Page < 1 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize is < 1 or > 200 ? 50 : queryParams.PageSize;

        var items = await query
            .OrderByDescending(r => r.EffectiveFrom)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<MetalRateDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<MetalRateDto> CreateManualRateAsync(CreateManualRateRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        if (request.RatePerGram <= 0m)
        {
            throw new BusinessRuleException("Rate per gram must be greater than zero.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        if (!branch.IsActive)
        {
            throw new BusinessRuleException($"Branch '{branch.Name}' is inactive.");
        }

        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken)
            ?? throw new NotFoundException("Material", request.MaterialId);

        if (!material.IsActive)
        {
            throw new BusinessRuleException($"Material '{material.Name}' is inactive.");
        }

        var purity = await _context.Purities.FirstOrDefaultAsync(p => p.Id == request.PurityId, cancellationToken)
            ?? throw new NotFoundException("Purity", request.PurityId);

        if (!purity.IsActive)
        {
            throw new BusinessRuleException($"Purity '{purity.Name}' is inactive.");
        }

        var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? branch.CurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();

        var now = DateTimeOffset.UtcNow;
        var effectiveFrom = request.EffectiveFrom ?? now;

        // Deactivate existing active rate for this tuple
        var existingActive = await _context.MetalRates
            .Where(r => r.BranchId == request.BranchId
                     && r.MaterialId == request.MaterialId
                     && r.PurityId == request.PurityId
                     && r.CurrencyCode == currencyCode
                     && r.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var r in existingActive)
        {
            r.IsActive = false;
            r.EffectiveTo = effectiveFrom;
        }

        var newRate = new MetalRate
        {
            BranchId = request.BranchId,
            MaterialId = request.MaterialId,
            PurityId = request.PurityId,
            CurrencyCode = currencyCode,
            RatePerGram = Math.Round(request.RatePerGram, 4),
            Source = MetalRateSources.Manual,
            ExternalReferenceRate = null,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = null,
            IsActive = true,
            IsOverride = false,
            OverrideReason = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            CreatedBy = _currentUserService.UserId
        };

        _context.MetalRates.Add(newRate);

        RecordAudit("MetalRate", newRate.Id, "MANUAL_RATE_CREATED", null, new
        {
            BranchCode = branch.Code,
            MaterialCode = material.Code,
            PurityCode = purity.Code,
            CurrencyCode = currencyCode,
            RatePerGram = newRate.RatePerGram,
            EffectiveFrom = newRate.EffectiveFrom
        }, branch.Id);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRateDtoByIdAsync(newRate.Id, cancellationToken);
    }

    public async Task<MetalRateDto> AdminOverrideRateAsync(AdminOverrideRateRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        if (request.OverrideRatePerGram <= 0m)
        {
            throw new BusinessRuleException("Override rate per gram must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.OverrideReason))
        {
            throw new BusinessRuleException("Override reason is mandatory for admin rate overrides.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        var material = await _context.Materials.FirstOrDefaultAsync(m => m.Id == request.MaterialId, cancellationToken)
            ?? throw new NotFoundException("Material", request.MaterialId);

        var purity = await _context.Purities.FirstOrDefaultAsync(p => p.Id == request.PurityId, cancellationToken)
            ?? throw new NotFoundException("Purity", request.PurityId);

        var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? branch.CurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();

        var now = DateTimeOffset.UtcNow;

        // Find current active rate to capture reference rate
        var existingActive = await _context.MetalRates
            .Where(r => r.BranchId == request.BranchId
                     && r.MaterialId == request.MaterialId
                     && r.PurityId == request.PurityId
                     && r.CurrencyCode == currencyCode
                     && r.IsActive)
            .ToListAsync(cancellationToken);

        decimal? externalRef = null;
        decimal? oldActiveRate = null;

        foreach (var r in existingActive)
        {
            r.IsActive = false;
            r.EffectiveTo = now;
            externalRef ??= r.ExternalReferenceRate ?? r.RatePerGram;
            oldActiveRate ??= r.RatePerGram;
        }

        var overrideRate = new MetalRate
        {
            BranchId = request.BranchId,
            MaterialId = request.MaterialId,
            PurityId = request.PurityId,
            CurrencyCode = currencyCode,
            RatePerGram = Math.Round(request.OverrideRatePerGram, 4),
            Source = MetalRateSources.AdminOverride,
            ExternalReferenceRate = externalRef,
            EffectiveFrom = now,
            EffectiveTo = null,
            IsActive = true,
            IsOverride = true,
            OverrideReason = request.OverrideReason.Trim(),
            CreatedAt = now,
            CreatedBy = _currentUserService.UserId
        };

        _context.MetalRates.Add(overrideRate);

        RecordAudit("MetalRate", overrideRate.Id, "RATE_ADMIN_OVERRIDE", new
        {
            PreviousRate = oldActiveRate,
            ExternalReferenceRate = externalRef
        }, new
        {
            BranchCode = branch.Code,
            MaterialCode = material.Code,
            PurityCode = purity.Code,
            CurrencyCode = currencyCode,
            OverrideRatePerGram = overrideRate.RatePerGram,
            Reason = overrideRate.OverrideReason
        }, branch.Id);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetRateDtoByIdAsync(overrideRate.Id, cancellationToken);
    }

    public async Task<List<MetalRateDto>> RefreshExternalRatesAsync(RefreshExternalRatesRequest request, CancellationToken cancellationToken = default)
    {
        var targetBranchId = request.BranchId ?? _currentUserService.BranchId;
        Branch? branch = null;

        if (targetBranchId.HasValue && targetBranchId != Guid.Empty)
        {
            await _branchAuthService.EnsureBranchAccessAsync(targetBranchId.Value, cancellationToken);
            branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == targetBranchId.Value, cancellationToken);
        }
        else if (_currentUserService.IsGlobalAdmin)
        {
            branch = await _context.Branches.FirstOrDefaultAsync(b => b.IsActive, cancellationToken);
        }

        if (branch == null)
        {
            throw new BusinessRuleException("Target branch must be specified for external rate refresh.");
        }

        var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? branch.CurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();

        var now = DateTimeOffset.UtcNow;
        _lastAttemptAt = now;

        var providerResult = await _metalRateProvider.FetchRatesAsync(currencyCode, cancellationToken);

        if (!providerResult.IsSuccess)
        {
            _lastErrorMessage = providerResult.ErrorMessage;
            _logger.LogWarning("External rate refresh failed: {Error}. Retaining existing active store rates.", providerResult.ErrorMessage);

            RecordAudit("MetalRateProvider", Guid.Empty, "EXTERNAL_REFRESH_FAILED", null, new
            {
                Provider = providerResult.ProviderName,
                Error = providerResult.ErrorMessage,
                Currency = currencyCode
            }, branch.Id);

            await _context.SaveChangesAsync(cancellationToken);

            // Return current active rates untouched
            return await GetActiveRatesAsync(branch.Id, currencyCode, cancellationToken);
        }

        _lastSuccessfulRefreshAt = now;
        _lastErrorMessage = null;

        // Find active Gold materials
        var goldMaterials = await _context.Materials
            .Where(m => m.IsActive && (m.Code.Contains("GOLD") || m.Name.Contains("Gold")))
            .ToListAsync(cancellationToken);

        if (goldMaterials.Count == 0)
        {
            goldMaterials = await _context.Materials.Where(m => m.IsActive).Take(1).ToListAsync(cancellationToken);
        }

        var purities = await _context.Purities.Where(p => p.IsActive).ToListAsync(cancellationToken);
        var goldRateNorm = providerResult.NormalizedRates.FirstOrDefault(r => r.MetalSymbol == "XAU");

        if (goldRateNorm == null)
        {
            throw new BusinessRuleException("Provider did not return normalized gold (XAU) rates.");
        }

        var newRateIds = new List<Guid>();

        foreach (var mat in goldMaterials)
        {
            foreach (var pur in purities)
            {
                decimal ratePerGram;
                // Check if provider supplied direct karat rate matching purity
                if (goldRateNorm.KaratRates != null && goldRateNorm.KaratRates.TryGetValue(pur.KaratValue, out var directRate))
                {
                    ratePerGram = directRate;
                }
                else
                {
                    ratePerGram = DerivePurityRate(goldRateNorm.RatePerGram24K, pur.PurityPercentage, pur.KaratValue);
                }

                // Deactivate current active rate
                var existingActive = await _context.MetalRates
                    .Where(r => r.BranchId == branch.Id
                             && r.MaterialId == mat.Id
                             && r.PurityId == pur.Id
                             && r.CurrencyCode == currencyCode
                             && r.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var r in existingActive)
                {
                    r.IsActive = false;
                    r.EffectiveTo = now;
                }

                var newRate = new MetalRate
                {
                    BranchId = branch.Id,
                    MaterialId = mat.Id,
                    PurityId = pur.Id,
                    CurrencyCode = currencyCode,
                    RatePerGram = Math.Round(ratePerGram, 4),
                    Source = MetalRateSources.ExternalApi,
                    ExternalReferenceRate = Math.Round(goldRateNorm.RatePerGram24K, 4),
                    EffectiveFrom = now,
                    EffectiveTo = null,
                    IsActive = true,
                    IsOverride = false,
                    OverrideReason = null,
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserId
                };

                _context.MetalRates.Add(newRate);
                newRateIds.Add(newRate.Id);
            }
        }

        RecordAudit("MetalRate", Guid.Empty, "EXTERNAL_RATES_REFRESHED", null, new
        {
            Provider = providerResult.ProviderName,
            BranchCode = branch.Code,
            CurrencyCode = currencyCode,
            Reference24K = goldRateNorm.RatePerGram24K,
            RatesUpdatedCount = newRateIds.Count
        }, branch.Id);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetActiveRatesAsync(branch.Id, currencyCode, cancellationToken);
    }

    public async Task<ExternalRateProviderStatusDto> GetProviderStatusAsync(CancellationToken cancellationToken = default)
    {
        var activeCount = await _context.MetalRates.CountAsync(r => r.IsActive, cancellationToken);
        var isAvailable = string.IsNullOrEmpty(_lastErrorMessage);

        return new ExternalRateProviderStatusDto(
            ProviderName: _metalRateProvider.ProviderName,
            IsAvailable: isAvailable,
            LastSuccessfulRefreshAt: _lastSuccessfulRefreshAt,
            LastAttemptAt: _lastAttemptAt,
            LastErrorMessage: _lastErrorMessage,
            TotalActiveRatesManaged: activeCount
        );
    }

    /// <summary>
    /// Authoritative domain derivation formula for subordinate metal purities:
    /// If purity percentage is present and > 0, calculates:
    ///   DerivedRate = pureReferenceRate * (purityPercentage / 100m)
    /// Otherwise falls back to karat proportion:
    ///   DerivedRate = pureReferenceRate * (karatValue / 24.0m)
    /// Returns rate rounded to 4 decimal places (numeric(18,4)).
    /// </summary>
    public decimal DerivePurityRate(decimal pureReferenceRate, decimal purityPercentage, decimal? karatValue)
    {
        if (pureReferenceRate <= 0m) return 0m;

        decimal derived;
        if (purityPercentage > 0m)
        {
            derived = pureReferenceRate * (purityPercentage / 100.0m);
        }
        else if (karatValue.HasValue && karatValue.Value > 0m)
        {
            derived = pureReferenceRate * (karatValue.Value / 24.0m);
        }
        else
        {
            derived = pureReferenceRate;
        }

        return Math.Round(derived, 4);
    }

    private async Task<MetalRateDto> GetRateDtoByIdAsync(Guid rateId, CancellationToken cancellationToken)
    {
        var rate = await _context.MetalRates
            .AsNoTracking()
            .Include(r => r.Branch)
            .Include(r => r.Material)
            .Include(r => r.Purity)
            .Include(r => r.Creator)
            .FirstOrDefaultAsync(r => r.Id == rateId, cancellationToken)
            ?? throw new NotFoundException("MetalRate", rateId);

        return MapToDto(rate);
    }

    private static MetalRateDto MapToDto(MetalRate r)
    {
        return new MetalRateDto(
            r.Id,
            r.BranchId,
            r.Branch?.Code ?? string.Empty,
            r.Branch?.Name ?? string.Empty,
            r.MaterialId,
            r.Material?.Code ?? string.Empty,
            r.Material?.Name ?? string.Empty,
            r.PurityId,
            r.Purity?.Code ?? string.Empty,
            r.Purity?.Name ?? string.Empty,
            r.Purity?.KaratValue ?? 0m,
            r.Purity?.PurityPercentage ?? 0m,
            r.CurrencyCode,
            r.RatePerGram,
            r.Source,
            r.ExternalReferenceRate,
            r.EffectiveFrom,
            r.EffectiveTo,
            r.IsActive,
            r.IsOverride,
            r.OverrideReason,
            r.CreatedAt,
            r.CreatedBy,
            r.Creator?.FullName
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

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
        
        // Use CBSL rate data if branch is in CBSL mode, otherwise Market gold
        var feed = await _metalRateProvider.FetchSriLankaFeedAsync(cancellationToken);
        ProviderNormalizedRate? goldRateNorm = null;
        string rateSource = MetalRateSources.ExternalApi;

        if (branch.RatePricingMode == BranchPricingModes.CbslApi && feed.IsSuccess && feed.CbslGold != null)
        {
            goldRateNorm = feed.CbslGold;
            rateSource = MetalRateSources.CbslApi;
        }
        else if (feed.IsSuccess && feed.MarketGold != null)
        {
            goldRateNorm = feed.MarketGold;
            rateSource = branch.RatePricingMode == BranchPricingModes.MarketApi ? MetalRateSources.MarketApi : MetalRateSources.ExternalApi;
        }
        else
        {
            goldRateNorm = providerResult.NormalizedRates.FirstOrDefault(r => r.MetalSymbol == "XAU");
        }

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
                    Source = rateSource,
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

    public async Task<MetalRateFeedPreviewDto> GetFeedPreviewAsync(CancellationToken cancellationToken = default)
    {
        var feed = await _metalRateProvider.FetchSriLankaFeedAsync(cancellationToken);

        var marketRates = new List<FeedPurityRateDto>();
        if (feed.MarketGold != null)
        {
            if (feed.MarketGold.KaratRates != null)
            {
                foreach (var (karat, rate) in feed.MarketGold.KaratRates.OrderByDescending(k => k.Key))
                {
                    decimal pawn = (karat == 24.0m && feed.MarketGold.RatePerPawn24K.HasValue)
                        ? feed.MarketGold.RatePerPawn24K.Value
                        : (karat == 22.0m && feed.MarketGold.RatePerPawn22K.HasValue)
                            ? feed.MarketGold.RatePerPawn22K.Value
                            : Math.Round(rate * 8m, 2);

                    var label = karat == Math.Floor(karat) ? $"{(int)karat}K" : $"{karat:G}K";
                    marketRates.Add(new FeedPurityRateDto(label, karat, rate, pawn));
                }
            }
            else
            {
                marketRates.Add(new FeedPurityRateDto("24K", 24m, feed.MarketGold.RatePerGram24K, Math.Round(feed.MarketGold.RatePerGram24K * 8m, 2)));
            }
        }

        var cbslRates = new List<FeedPurityRateDto>();
        if (feed.CbslGold != null)
        {
            if (feed.CbslGold.KaratRates != null)
            {
                foreach (var (karat, rate) in feed.CbslGold.KaratRates.OrderByDescending(k => k.Key))
                {
                    decimal pawn = (karat == 24.0m && feed.CbslGold.RatePerPawn24K.HasValue)
                        ? feed.CbslGold.RatePerPawn24K.Value
                        : (karat == 22.0m && feed.CbslGold.RatePerPawn22K.HasValue)
                            ? feed.CbslGold.RatePerPawn22K.Value
                            : Math.Round(rate * 8m, 2);

                    var label = karat == Math.Floor(karat) ? $"{(int)karat}K" : $"{karat:G}K";
                    cbslRates.Add(new FeedPurityRateDto(label, karat, rate, pawn));
                }
            }
            else
            {
                cbslRates.Add(new FeedPurityRateDto("24K", 24m, feed.CbslGold.RatePerGram24K, Math.Round(feed.CbslGold.RatePerGram24K * 8m, 2)));
            }
        }

        return new MetalRateFeedPreviewDto(
            IsSuccess: feed.IsSuccess,
            ProviderName: feed.ProviderName,
            Currency: feed.Currency,
            UpdatedAt: feed.UpdatedAt,
            MarketTime: feed.MarketTime,
            CbslDate: feed.CbslDate,
            MarketRates: marketRates,
            CbslRates: cbslRates,
            AttributionText: feed.AttributionText,
            AttributionUrl: feed.AttributionUrl,
            ErrorMessage: feed.ErrorMessage
        );
    }

    public async Task<List<MetalRateDto>> SwitchPricingModeAsync(SwitchPricingModeRequest request, CancellationToken cancellationToken = default)
    {
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        if (!BranchPricingModes.All.Contains(request.PricingMode))
        {
            throw new BusinessRuleException($"Invalid pricing mode '{request.PricingMode}'. Supported modes: {string.Join(", ", BranchPricingModes.All)}.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        if (!branch.IsActive)
        {
            throw new BusinessRuleException($"Branch '{branch.Name}' is inactive.");
        }

        var currencyCode = branch.CurrencyCode.ToUpperInvariant();
        if (request.PricingMode != BranchPricingModes.Manual && currencyCode != "LKR")
        {
            throw new BusinessRuleException($"Live Sri Lankan gold rate provider is only available for LKR currency branches. Branch currency is {currencyCode}.");
        }

        var previousMode = branch.RatePricingMode;
        var now = DateTimeOffset.UtcNow;

        // Query active Gold materials & purities
        var goldMaterials = await _context.Materials
            .Where(m => m.IsActive && (m.Code.Contains("GOLD") || m.Name.Contains("Gold")))
            .ToListAsync(cancellationToken);

        if (goldMaterials.Count == 0)
        {
            goldMaterials = await _context.Materials.Where(m => m.IsActive).Take(1).ToListAsync(cancellationToken);
        }

        var purities = await _context.Purities.Where(p => p.IsActive).OrderByDescending(p => p.KaratValue).ToListAsync(cancellationToken);

        if (request.PricingMode == BranchPricingModes.MarketApi || request.PricingMode == BranchPricingModes.CbslApi)
        {
            var feed = await _metalRateProvider.FetchSriLankaFeedAsync(cancellationToken);
            if (!feed.IsSuccess)
            {
                throw new BusinessRuleException($"Cannot switch to {request.PricingMode} mode because the live rate provider returned an error: {feed.ErrorMessage}. Current rates and mode remain unchanged.");
            }

            var rateData = request.PricingMode == BranchPricingModes.MarketApi ? feed.MarketGold : feed.CbslGold;
            if (rateData == null || rateData.RatePerGram24K <= 0m)
            {
                throw new BusinessRuleException($"Provider did not return valid {request.PricingMode} rates. Current rates and mode remain unchanged.");
            }

            var rateSource = request.PricingMode == BranchPricingModes.MarketApi ? MetalRateSources.MarketApi : MetalRateSources.CbslApi;

            // Atomic update: deactivate existing active rates for gold
            foreach (var mat in goldMaterials)
            {
                foreach (var pur in purities)
                {
                    decimal ratePerGram;
                    if (rateData.KaratRates != null && rateData.KaratRates.TryGetValue(pur.KaratValue, out var directRate))
                    {
                        ratePerGram = directRate;
                    }
                    else
                    {
                        ratePerGram = DerivePurityRate(rateData.RatePerGram24K, pur.PurityPercentage, pur.KaratValue);
                    }

                    if (ratePerGram <= 0m)
                    {
                        throw new BusinessRuleException($"Calculated rate for purity '{pur.Name}' is invalid (zero or negative). Mode switch aborted.");
                    }

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
                        Source = rateSource,
                        ExternalReferenceRate = Math.Round(rateData.RatePerGram24K, 4),
                        EffectiveFrom = now,
                        EffectiveTo = null,
                        IsActive = true,
                        IsOverride = false,
                        OverrideReason = request.Reason,
                        CreatedAt = now,
                        CreatedBy = _currentUserService.UserId
                    };

                    _context.MetalRates.Add(newRate);
                }
            }

            branch.RatePricingMode = request.PricingMode;
            _lastSuccessfulRefreshAt = now;
            _lastErrorMessage = null;
        }
        else // MANUAL mode
        {
            var manualRatesToApply = new Dictionary<(Guid MaterialId, Guid PurityId), decimal>();

            // Check if user provided initial manual rates in request
            if (request.InitialManualRates != null && request.InitialManualRates.Count > 0)
            {
                foreach (var item in request.InitialManualRates)
                {
                    if (item.RatePerGram > 0m)
                    {
                        manualRatesToApply[(item.MaterialId, item.PurityId)] = item.RatePerGram;
                    }
                }
            }

            var missingPurities = new List<string>();

            foreach (var mat in goldMaterials)
            {
                foreach (var pur in purities)
                {
                    if (manualRatesToApply.ContainsKey((mat.Id, pur.Id)))
                    {
                        continue;
                    }

                    // Look up most recent historical manual/override rate
                    var previousManual = await _context.MetalRates
                        .AsNoTracking()
                        .Where(r => r.BranchId == branch.Id
                                 && r.MaterialId == mat.Id
                                 && r.PurityId == pur.Id
                                 && r.CurrencyCode == currencyCode
                                 && (r.Source == MetalRateSources.Manual || r.Source == MetalRateSources.AdminOverride))
                        .OrderByDescending(r => r.EffectiveFrom)
                        .ThenByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (previousManual != null && previousManual.RatePerGram > 0m)
                    {
                        manualRatesToApply[(mat.Id, pur.Id)] = previousManual.RatePerGram;
                    }
                }
            }

            // If some purities lack historical manual rates, attempt derivation from known manual rates
            var knownManual = manualRatesToApply.FirstOrDefault(kv => kv.Value > 0m);
            if (knownManual.Value > 0m)
            {
                var knownPurity = purities.FirstOrDefault(p => p.Id == knownManual.Key.PurityId);
                decimal base24k;
                if (knownPurity != null && knownPurity.KaratValue == 24.0m)
                {
                    base24k = knownManual.Value;
                }
                else if (knownPurity != null && knownPurity.PurityPercentage > 0m)
                {
                    base24k = Math.Round(knownManual.Value / (knownPurity.PurityPercentage / 100.0m), 4);
                }
                else
                {
                    base24k = knownManual.Value;
                }

                foreach (var mat in goldMaterials)
                {
                    foreach (var pur in purities)
                    {
                        if (!manualRatesToApply.ContainsKey((mat.Id, pur.Id)))
                        {
                            manualRatesToApply[(mat.Id, pur.Id)] = DerivePurityRate(base24k, pur.PurityPercentage, pur.KaratValue);
                        }
                    }
                }
            }

            // Check if any purities are still missing
            foreach (var mat in goldMaterials)
            {
                foreach (var pur in purities)
                {
                    if (!manualRatesToApply.ContainsKey((mat.Id, pur.Id)) || manualRatesToApply[(mat.Id, pur.Id)] <= 0m)
                    {
                        missingPurities.Add($"{mat.Name} ({pur.Name})");
                    }
                }
            }

            if (missingPurities.Count > 0)
            {
                throw new BusinessRuleException($"No previous manual rates found for: {string.Join(", ", missingPurities)}. Please configure manual rates for these purities before activating manual mode.");
            }

            // Apply restored manual rates
            foreach (var mat in goldMaterials)
            {
                foreach (var pur in purities)
                {
                    var ratePerGram = manualRatesToApply[(mat.Id, pur.Id)];

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
                        Source = MetalRateSources.Manual,
                        ExternalReferenceRate = null,
                        EffectiveFrom = now,
                        EffectiveTo = null,
                        IsActive = true,
                        IsOverride = false,
                        OverrideReason = string.IsNullOrWhiteSpace(request.Reason) ? "Restored manual pricing mode" : request.Reason.Trim(),
                        CreatedAt = now,
                        CreatedBy = _currentUserService.UserId
                    };

                    _context.MetalRates.Add(newRate);
                }
            }

            branch.RatePricingMode = BranchPricingModes.Manual;
        }

        RecordAudit("Branch", branch.Id, "PRICING_MODE_SWITCHED", new
        {
            PreviousMode = previousMode
        }, new
        {
            NewMode = branch.RatePricingMode,
            Reason = request.Reason,
            BranchCode = branch.Code
        }, branch.Id);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetActiveRatesAsync(branch.Id, currencyCode, cancellationToken);
    }

    public async Task RefreshStaleBranchRatesAsync(CancellationToken cancellationToken = default)
    {
        var branchesInApiMode = await _context.Branches
            .Where(b => b.IsActive && (b.RatePricingMode == BranchPricingModes.MarketApi || b.RatePricingMode == BranchPricingModes.CbslApi))
            .ToListAsync(cancellationToken);

        if (branchesInApiMode.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        var feed = await _metalRateProvider.FetchSriLankaFeedAsync(cancellationToken);
        if (!feed.IsSuccess)
        {
            _logger.LogWarning("Background rate refresh skipped: {Error}", feed.ErrorMessage);
            return;
        }

        foreach (var branch in branchesInApiMode)
        {
            try
            {
                // Check latest active rate age
                var latestRate = await _context.MetalRates
                    .Where(r => r.BranchId == branch.Id && r.IsActive)
                    .OrderByDescending(r => r.EffectiveFrom)
                    .FirstOrDefaultAsync(cancellationToken);

                // If rates are newer than 15 minutes, skip
                if (latestRate != null && (now - latestRate.EffectiveFrom) < TimeSpan.FromMinutes(15))
                {
                    continue;
                }

                var rateData = branch.RatePricingMode == BranchPricingModes.MarketApi ? feed.MarketGold : feed.CbslGold;
                if (rateData == null || rateData.RatePerGram24K <= 0m) continue;

                var rateSource = branch.RatePricingMode == BranchPricingModes.MarketApi ? MetalRateSources.MarketApi : MetalRateSources.CbslApi;
                var currencyCode = branch.CurrencyCode.ToUpperInvariant();

                var goldMaterials = await _context.Materials
                    .Where(m => m.IsActive && (m.Code.Contains("GOLD") || m.Name.Contains("Gold")))
                    .ToListAsync(cancellationToken);
                if (goldMaterials.Count == 0)
                {
                    goldMaterials = await _context.Materials.Where(m => m.IsActive).Take(1).ToListAsync(cancellationToken);
                }

                var purities = await _context.Purities.Where(p => p.IsActive).ToListAsync(cancellationToken);

                foreach (var mat in goldMaterials)
                {
                    foreach (var pur in purities)
                    {
                        decimal ratePerGram;
                        if (rateData.KaratRates != null && rateData.KaratRates.TryGetValue(pur.KaratValue, out var directRate))
                        {
                            ratePerGram = directRate;
                        }
                        else
                        {
                            ratePerGram = DerivePurityRate(rateData.RatePerGram24K, pur.PurityPercentage, pur.KaratValue);
                        }

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
                            Source = rateSource,
                            ExternalReferenceRate = Math.Round(rateData.RatePerGram24K, 4),
                            EffectiveFrom = now,
                            EffectiveTo = null,
                            IsActive = true,
                            IsOverride = false,
                            CreatedAt = now
                        };

                        _context.MetalRates.Add(newRate);
                    }
                }

                _logger.LogInformation("Auto-refreshed {Mode} rates for branch {Code}.", branch.RatePricingMode, branch.Code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-refreshing rates for branch {Code}", branch.Code);
            }
        }

        _lastSuccessfulRefreshAt = now;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExternalRateProviderStatusDto> GetProviderStatusAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var targetBranchId = branchId ?? _currentUserService.BranchId;
        Branch? branch = null;
        if (targetBranchId.HasValue && targetBranchId != Guid.Empty)
        {
            branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == targetBranchId.Value, cancellationToken);
        }

        var activeCount = await _context.MetalRates.CountAsync(r => r.IsActive, cancellationToken);
        var isAvailable = string.IsNullOrEmpty(_lastErrorMessage);

        var feed = await _metalRateProvider.FetchSriLankaFeedAsync(cancellationToken);

        var activeMode = branch?.RatePricingMode ?? BranchPricingModes.Manual;

        // Check if rates are stale (only applies to API modes)
        bool isStale = false;
        if (branch != null && activeMode != BranchPricingModes.Manual)
        {
            var latestRate = await _context.MetalRates
                .Where(r => r.BranchId == branch.Id && r.IsActive)
                .OrderByDescending(r => r.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestRate == null || (DateTimeOffset.UtcNow - latestRate.EffectiveFrom) > TimeSpan.FromMinutes(15))
            {
                isStale = true;
            }
        }

        return new ExternalRateProviderStatusDto(
            ProviderName: _metalRateProvider.ProviderName,
            IsAvailable: isAvailable && feed.IsSuccess,
            LastSuccessfulRefreshAt: _lastSuccessfulRefreshAt ?? (feed.IsSuccess ? feed.UpdatedAt : null),
            LastAttemptAt: _lastAttemptAt ?? feed.UpdatedAt,
            LastErrorMessage: _lastErrorMessage ?? (feed.IsSuccess ? null : feed.ErrorMessage),
            TotalActiveRatesManaged: activeCount,
            ActivePricingMode: activeMode,
            ProviderEffectiveTime: feed.MarketTime,
            CbslReferenceDate: feed.CbslDate,
            AttributionText: feed.AttributionText,
            AttributionUrl: feed.AttributionUrl,
            IsStale: isStale
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

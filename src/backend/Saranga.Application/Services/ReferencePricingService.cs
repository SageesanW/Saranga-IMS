using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Rates;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;

namespace Saranga.Application.Services;

public class ReferencePricingService : IReferencePricingService
{
    private readonly IAppDbContext _context;
    private readonly IMetalRateService _metalRateService;
    private readonly IVatConfigurationService _vatConfigurationService;

    public ReferencePricingService(
        IAppDbContext context,
        IMetalRateService metalRateService,
        IVatConfigurationService vatConfigurationService)
    {
        _context = context;
        _metalRateService = metalRateService;
        _vatConfigurationService = vatConfigurationService;
    }

    public async Task<ReferencePriceBreakdownDto> CalculateReferencePriceAsync(CalculateReferencePriceRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NetWeight < 0m)
        {
            throw new BusinessRuleException("Net weight cannot be negative.");
        }

        if (request.PieceCount < 1)
        {
            throw new BusinessRuleException("Piece count must be at least 1.");
        }

        if (request.MakingChargeValue < 0m)
        {
            throw new BusinessRuleException("Making charge value cannot be negative.");
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

        // 1. Fetch current active metal rate
        var activeRate = await _metalRateService.GetActiveRateAsync(
            request.BranchId, request.MaterialId, request.PurityId, currencyCode, cancellationToken);

        // 2. Reference Material Value = NetWeight * RatePerGram
        var referenceMaterialValue = Math.Round(request.NetWeight * activeRate.RatePerGram, 2);

        // 3. Making Charge Calculation:
        //    PER_GRAM (1) = MakingChargeValue * NetWeight
        //    PER_ITEM (2) = MakingChargeValue * PieceCount
        decimal makingChargeAmount;
        string makingChargeMethodName;

        if (request.MakingChargeMethod == 1) // PER_GRAM
        {
            makingChargeMethodName = "PER_GRAM";
            makingChargeAmount = Math.Round(request.MakingChargeValue * request.NetWeight, 2);
        }
        else if (request.MakingChargeMethod == 2) // PER_ITEM
        {
            makingChargeMethodName = "PER_ITEM";
            makingChargeAmount = Math.Round(request.MakingChargeValue * request.PieceCount, 2);
        }
        else
        {
            throw new BusinessRuleException($"Invalid MakingChargeMethod '{request.MakingChargeMethod}'. Supported methods: 1 (PER_GRAM), 2 (PER_ITEM).");
        }

        // 4. Subtotal before Tax = Material Value + Making Charge
        var subtotalBeforeTax = referenceMaterialValue + makingChargeAmount;

        // 5. VAT Calculation
        var vatConfig = await _vatConfigurationService.GetActiveVatConfigurationAsync(request.BranchId, cancellationToken);
        decimal vatAmount = 0.00m;

        if (vatConfig.IsEnabled && vatConfig.RatePercentage > 0m)
        {
            vatAmount = Math.Round(subtotalBeforeTax * (vatConfig.RatePercentage / 100.0m), 2);
        }

        // 6. Reference Total
        var referenceTotal = subtotalBeforeTax + vatAmount;

        return new ReferencePriceBreakdownDto(
            BranchId: branch.Id,
            BranchCode: branch.Code,
            CurrencyCode: currencyCode,
            MaterialId: material.Id,
            MaterialName: material.Name,
            PurityId: purity.Id,
            PurityName: purity.Name,
            PurityKarat: purity.KaratValue,
            RatePerGramUsed: activeRate.RatePerGram,
            RateSource: activeRate.Source,
            IsRateOverridden: activeRate.IsOverride,
            RateOverrideReason: activeRate.OverrideReason,
            NetWeight: request.NetWeight,
            ReferenceMaterialValue: referenceMaterialValue,
            MakingChargeMethod: request.MakingChargeMethod,
            MakingChargeMethodName: makingChargeMethodName,
            MakingChargeValue: request.MakingChargeValue,
            PieceCount: request.PieceCount,
            MakingChargeAmount: makingChargeAmount,
            SubtotalBeforeTax: subtotalBeforeTax,
            VatEnabled: vatConfig.IsEnabled,
            VatRatePercentage: vatConfig.IsEnabled ? vatConfig.RatePercentage : 0.00m,
            VatAmount: vatAmount,
            ReferenceTotal: referenceTotal,
            CalculatedAt: DateTimeOffset.UtcNow
        );
    }
}

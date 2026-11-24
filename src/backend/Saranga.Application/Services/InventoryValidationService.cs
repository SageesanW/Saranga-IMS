using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Inventory;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public class InventoryValidationService : IInventoryValidationService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public InventoryValidationService(IAppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public string? ValidateItemName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Item name cannot be empty.";
        }
        return null;
    }

    public (bool IsValid, decimal NetWeight, string? Error) ValidateWeights(decimal grossWeight, decimal stoneWeight)
    {
        if (grossWeight <= 0)
        {
            return (false, 0, "Gross weight must be greater than zero.");
        }
        if (stoneWeight < 0)
        {
            return (false, 0, "Stone weight cannot be negative.");
        }
        if (stoneWeight > grossWeight)
        {
            return (false, 0, $"Stone Weight ({stoneWeight}g) cannot exceed Gross Weight ({grossWeight}g).");
        }

        var netWeight = grossWeight - stoneWeight;
        if (netWeight < 0)
        {
            return (false, 0, "Net weight cannot be negative.");
        }

        return (true, netWeight, null);
    }

    public string? ValidatePieceCount(int pieceCount)
    {
        if (pieceCount < 1)
        {
            return "Piece count must be at least 1.";
        }
        return null;
    }

    public (bool Success, MakingChargeMethod Method, string? Error) ParseAndValidateMakingChargeMethod(string? rawMethod)
    {
        var norm = rawMethod?.Trim().ToUpperInvariant() ?? string.Empty;
        if (norm == "PER_GRAM" || norm == "PERGRAM" || norm == "PER GRAM" || norm == "1")
        {
            return (true, MakingChargeMethod.PerGram, null);
        }
        if (norm == "PER_ITEM" || norm == "PERITEM" || norm == "PER ITEM" || norm == "FIXED" || norm == "2")
        {
            return (true, MakingChargeMethod.PerItem, null);
        }
        return (false, MakingChargeMethod.PerGram, $"Invalid making charge method \"{rawMethod}\". Allowed: PER_GRAM, PER_ITEM (or FIXED).");
    }

    public string? ValidateMakingCharge(MakingChargeMethod method, decimal value)
    {
        if (!Enum.IsDefined(typeof(MakingChargeMethod), method))
        {
            return "Invalid making charge method.";
        }
        if (value < 0)
        {
            return "Making charge value cannot be negative.";
        }
        return null;
    }

    public string? ValidateStockLocationBranch(Guid locationBranchId, Guid expectedBranchId, string? locationCode, string? branchCode)
    {
        if (locationBranchId != expectedBranchId)
        {
            return $"Stock location '{locationCode}' belongs to a different branch and does not belong to selected branch '{branchCode}'.";
        }
        return null;
    }

    public string? ValidateEntityActive(string entityType, string identifier, bool isActive)
    {
        if (!isActive)
        {
            return $"{entityType} '{identifier}' is inactive.";
        }
        return null;
    }

    public string? ValidateSpecification(Specification spec, Guid? optionId, string? customValue)
    {
        if (spec == null || !spec.IsActive)
        {
            return $"Specification '{(spec?.Name ?? "Unknown")}' is invalid or inactive.";
        }

        var inputType = string.IsNullOrWhiteSpace(spec.InputType) ? SpecificationInputTypes.Predefined : spec.InputType.Trim().ToUpperInvariant();

        if (inputType == SpecificationInputTypes.Predefined)
        {
            if (!optionId.HasValue)
            {
                return $"Specification '{spec.Name}' requires a predefined option to be selected.";
            }
            if (!string.IsNullOrWhiteSpace(customValue))
            {
                return $"Specification '{spec.Name}' uses predefined options only; CustomValue is disallowed.";
            }
            var opt = spec.Options.FirstOrDefault(o => o.Id == optionId.Value);
            if (opt == null || !opt.IsActive)
            {
                return $"Specification option is invalid or inactive for specification '{spec.Name}'.";
            }
        }
        else if (inputType == SpecificationInputTypes.FreeText)
        {
            if (optionId.HasValue)
            {
                return $"Specification '{spec.Name}' is configured for free-text only; selecting a predefined option is disallowed.";
            }
            if (string.IsNullOrWhiteSpace(customValue))
            {
                return $"Specification '{spec.Name}' requires a custom value.";
            }
        }
        else if (inputType == SpecificationInputTypes.Hybrid)
        {
            if (!optionId.HasValue && string.IsNullOrWhiteSpace(customValue))
            {
                return $"Specification '{spec.Name}' requires either a predefined option or a custom value.";
            }
            if (optionId.HasValue)
            {
                var opt = spec.Options.FirstOrDefault(o => o.Id == optionId.Value);
                if (opt == null || !opt.IsActive)
                {
                    return $"Specification option is invalid or inactive for specification '{spec.Name}'.";
                }
            }
        }

        return null;
    }

    public async Task<ValidatedInventoryItemData> ValidateManualItemCreationAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default)
    {
        var nameErr = ValidateItemName(request.Name);
        if (nameErr != null)
        {
            throw new BusinessRuleException(nameErr);
        }

        // Branch Validation
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        if (branch == null || !branch.IsActive)
        {
            throw new BusinessRuleException("Target branch does not exist or is inactive.");
        }

        // Stock Location Validation
        var location = await _context.StockLocations.FirstOrDefaultAsync(l => l.Id == request.StockLocationId, cancellationToken);
        if (location == null)
        {
            throw new BusinessRuleException("Specified stock location does not exist.");
        }
        var branchMismatch = ValidateStockLocationBranch(location.BranchId, request.BranchId, location.Code, branch.Code);
        if (branchMismatch != null)
        {
            throw new BusinessRuleException(branchMismatch);
        }
        var locActiveErr = ValidateEntityActive("Stock location", location.Name, location.IsActive);
        if (locActiveErr != null)
        {
            throw new BusinessRuleException(locActiveErr);
        }

        // Master Entities Validation
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

        // Weights
        var weightResult = ValidateWeights(request.GrossWeight, request.StoneWeight);
        if (!weightResult.IsValid)
        {
            throw new BusinessRuleException(weightResult.Error!);
        }

        // Piece Count
        var pieceCountErr = ValidatePieceCount(request.PieceCount);
        if (pieceCountErr != null)
        {
            throw new BusinessRuleException(pieceCountErr);
        }

        // Making Charge
        var mcErr = ValidateMakingCharge((MakingChargeMethod)request.MakingChargeMethod, request.MakingChargeValue);
        if (mcErr != null)
        {
            throw new BusinessRuleException(mcErr);
        }

        // Specifications
        var itemSpecs = new List<InventoryItemSpecification>();
        if (request.Specifications != null && request.Specifications.Count > 0)
        {
            foreach (var specReq in request.Specifications)
            {
                var spec = await _context.Specifications
                    .Include(s => s.Options)
                    .FirstOrDefaultAsync(s => s.Id == specReq.SpecificationId, cancellationToken);

                if (spec == null || !spec.IsActive)
                {
                    throw new BusinessRuleException($"Specification with ID '{specReq.SpecificationId}' is invalid or inactive.");
                }

                var specErr = ValidateSpecification(spec, specReq.SpecificationOptionId, specReq.CustomValue);
                if (specErr != null)
                {
                    throw new BusinessRuleException(specErr);
                }

                itemSpecs.Add(new InventoryItemSpecification
                {
                    SpecificationId = specReq.SpecificationId,
                    SpecificationOptionId = specReq.SpecificationOptionId,
                    CustomValue = specReq.CustomValue?.Trim(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = _currentUserService.UserId
                });
            }
        }

        return new ValidatedInventoryItemData(branch, location, material, category, purity, weightResult.NetWeight, itemSpecs);
    }

    public async Task<ValidatedInventoryItemUpdateData> ValidateManualItemUpdateAsync(UpdateInventoryItemRequest request, Guid branchId, CancellationToken cancellationToken = default)
    {
        var nameErr = ValidateItemName(request.Name);
        if (nameErr != null)
        {
            throw new BusinessRuleException(nameErr);
        }

        var location = await _context.StockLocations.FirstOrDefaultAsync(l => l.Id == request.StockLocationId, cancellationToken);
        if (location == null)
        {
            throw new BusinessRuleException("Specified stock location does not exist.");
        }
        var branchMismatch = ValidateStockLocationBranch(location.BranchId, branchId, location.Code, branchId.ToString());
        if (branchMismatch != null)
        {
            throw new BusinessRuleException($"Stock location '{location.Code}' belongs to a different branch and cannot be assigned to this item.");
        }
        var locActiveErr = ValidateEntityActive("Stock location", location.Name, location.IsActive);
        if (locActiveErr != null)
        {
            throw new BusinessRuleException(locActiveErr);
        }

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

        var weightResult = ValidateWeights(request.GrossWeight, request.StoneWeight);
        if (!weightResult.IsValid)
        {
            throw new BusinessRuleException(weightResult.Error!);
        }

        var pieceCountErr = ValidatePieceCount(request.PieceCount);
        if (pieceCountErr != null)
        {
            throw new BusinessRuleException(pieceCountErr);
        }

        var mcErr = ValidateMakingCharge((MakingChargeMethod)request.MakingChargeMethod, request.MakingChargeValue);
        if (mcErr != null)
        {
            throw new BusinessRuleException(mcErr);
        }

        var itemSpecs = new List<InventoryItemSpecification>();
        if (request.Specifications != null && request.Specifications.Count > 0)
        {
            foreach (var specReq in request.Specifications)
            {
                var spec = await _context.Specifications
                    .Include(s => s.Options)
                    .FirstOrDefaultAsync(s => s.Id == specReq.SpecificationId, cancellationToken);

                if (spec == null || !spec.IsActive)
                {
                    throw new BusinessRuleException($"Specification with ID '{specReq.SpecificationId}' is invalid or inactive.");
                }

                var specErr = ValidateSpecification(spec, specReq.SpecificationOptionId, specReq.CustomValue);
                if (specErr != null)
                {
                    throw new BusinessRuleException(specErr);
                }

                itemSpecs.Add(new InventoryItemSpecification
                {
                    SpecificationId = specReq.SpecificationId,
                    SpecificationOptionId = specReq.SpecificationOptionId,
                    CustomValue = specReq.CustomValue?.Trim(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = _currentUserService.UserId
                });
            }
        }

        return new ValidatedInventoryItemUpdateData(location, material, category, purity, weightResult.NetWeight, itemSpecs);
    }
}

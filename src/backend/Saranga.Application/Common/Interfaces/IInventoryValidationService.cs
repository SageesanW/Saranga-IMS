using Saranga.Application.DTOs.Inventory;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Common.Interfaces;

public interface IInventoryValidationService
{
    /// <summary>
    /// Validates item name rules.
    /// </summary>
    string? ValidateItemName(string? name);

    /// <summary>
    /// Validates Gross and Stone weights and computes Net weight.
    /// Rules: GrossWeight > 0, StoneWeight >= 0, StoneWeight <= GrossWeight, NetWeight = Gross - Stone >= 0.
    /// </summary>
    (bool IsValid, decimal NetWeight, string? Error) ValidateWeights(decimal grossWeight, decimal stoneWeight);

    /// <summary>
    /// Validates piece count rule: PieceCount >= 1.
    /// </summary>
    string? ValidatePieceCount(int pieceCount);

    /// <summary>
    /// Parses string representation of making charge method into domain enum.
    /// Supports PER_GRAM, PER_ITEM (or FIXED), and numeric values.
    /// </summary>
    (bool Success, MakingChargeMethod Method, string? Error) ParseAndValidateMakingChargeMethod(string? rawMethod);

    /// <summary>
    /// Validates making charge method enum and value: Method is valid enum, Value >= 0.
    /// </summary>
    string? ValidateMakingCharge(MakingChargeMethod method, decimal value);

    /// <summary>
    /// Validates that a stock location belongs to the target branch.
    /// </summary>
    string? ValidateStockLocationBranch(Guid locationBranchId, Guid expectedBranchId, string? locationCode, string? branchCode);

    /// <summary>
    /// Validates whether a referenced master entity is active.
    /// </summary>
    string? ValidateEntityActive(string entityType, string identifier, bool isActive);

    /// <summary>
    /// Validates specification input rules (Predefined vs FreeText vs Hybrid).
    /// </summary>
    string? ValidateSpecification(Specification spec, Guid? optionId, string? customValue);

    /// <summary>
    /// Orchestrates full domain validation for manual single-item inventory creation.
    /// Throws BusinessRuleException if any rule fails.
    /// </summary>
    Task<ValidatedInventoryItemData> ValidateManualItemCreationAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates domain validation for manual single-item inventory updates.
    /// Throws BusinessRuleException if any rule fails.
    /// </summary>
    Task<ValidatedInventoryItemUpdateData> ValidateManualItemUpdateAsync(UpdateInventoryItemRequest request, Guid branchId, CancellationToken cancellationToken = default);
}

public record ValidatedInventoryItemData(
    Branch Branch,
    StockLocation StockLocation,
    Material Material,
    Category Category,
    Purity Purity,
    decimal NetWeight,
    List<InventoryItemSpecification> Specifications
);

public record ValidatedInventoryItemUpdateData(
    StockLocation StockLocation,
    Material Material,
    Category Category,
    Purity Purity,
    decimal NetWeight,
    List<InventoryItemSpecification> Specifications
);

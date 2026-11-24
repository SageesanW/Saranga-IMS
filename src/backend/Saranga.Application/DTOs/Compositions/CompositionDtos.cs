using Saranga.Application.DTOs.Common;

namespace Saranga.Application.DTOs.Compositions;

public record CompositionMemberDto(
    Guid Id,
    Guid InventoryItemId,
    string ItemName,
    string ItemCode,
    string TagNumber,
    string Barcode,
    Guid CategoryId,
    string CategoryName,
    string CategoryCode,
    string MaterialName,
    decimal PurityKarat,
    decimal GrossWeight,
    decimal StoneWeight,
    decimal NetWeight,
    int PieceCount,
    int Status,
    string StatusName,
    int SortOrder,
    bool IsActive,
    DateTimeOffset AddedAt,
    DateTimeOffset? RemovedAt
);

public record InventoryCompositionDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string BranchCode,
    Guid StockLocationId,
    string StockLocationName,
    string StockLocationCode,
    string Name,
    int Type,
    string TypeName,
    string TagNumber,
    string Barcode,
    int Status,
    string StatusName,
    string Description,
    bool IsActive,
    decimal TotalGrossWeight,
    decimal TotalStoneWeight,
    decimal TotalNetWeight,
    int TotalPieceCount,
    int ActiveMemberCount,
    int TotalMemberCount,
    List<CompositionMemberDto> Members,
    uint Version,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset? UpdatedAt
);

public record CreateCompositionRequest(
    Guid BranchId,
    Guid StockLocationId,
    string Name,
    int Type, // 1 = Assembly, 2 = Group
    string? Description,
    string? TagNumber,
    string? Barcode,
    List<Guid> ChildItemIds
);

public record AddCompositionMemberRequest(
    Guid InventoryItemId,
    int? SortOrder
);

public record CompositionQueryParams(
    Guid? BranchId = null,
    Guid? StockLocationId = null,
    int? Type = null,
    int? Status = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20
);

public record EligibleChildQueryParams(
    Guid BranchId,
    Guid StockLocationId,
    Guid? CategoryId = null,
    string? SearchTerm = null
);

public record EligibleChildItemDto(
    Guid Id,
    string TagNumber,
    string Barcode,
    string ItemCode,
    string Name,
    Guid CategoryId,
    string CategoryName,
    string CategoryCode,
    string MaterialName,
    decimal PurityKarat,
    decimal GrossWeight,
    decimal StoneWeight,
    decimal NetWeight,
    int PieceCount
);

using Saranga.Application.DTOs.Common;

namespace Saranga.Application.DTOs.Inventory;

public record InventoryItemDto(
    Guid Id,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    Guid StockLocationId,
    string LocationCode,
    string LocationName,
    string LocationType,
    string Name,
    string ItemCode,
    string TagNumber,
    string Barcode,
    Guid MaterialId,
    string MaterialCode,
    string MaterialName,
    Guid CategoryId,
    string CategoryCode,
    string CategoryName,
    Guid PurityId,
    string PurityCode,
    string PurityName,
    decimal KaratValue,
    decimal PurityPercentage,
    decimal GrossWeight,
    decimal StoneWeight,
    decimal NetWeight,
    int PieceCount,
    int MakingChargeMethod,
    string MakingChargeMethodName,
    decimal MakingChargeValue,
    int Status,
    string StatusName,
    string Description,
    bool IsActive,
    uint Version,
    List<InventoryItemSpecificationDto> Specifications,
    List<InventoryItemMediaDto> Media,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
);

public record InventoryItemSpecificationDto(
    Guid Id,
    Guid SpecificationId,
    string SpecificationCode,
    string SpecificationName,
    Guid? SpecificationOptionId,
    string? OptionCode,
    string? OptionValue,
    string? CustomValue
);

public record InventoryItemMediaDto(
    Guid Id,
    string FileName,
    string StoragePath,
    string ContentType,
    long FileSize,
    int MediaType,
    string MediaTypeName,
    int SortOrder,
    bool IsPrimary,
    DateTimeOffset UploadedAt
);

public record CreateInventoryItemMediaRequest(
    string FileName,
    string StoragePath,
    string ContentType,
    long FileSize,
    int MediaType = 1,
    int SortOrder = 0,
    bool IsPrimary = false
);

public record FileUploadResultDto(
    string FileName,
    string StoragePath,
    string ContentType,
    long FileSize,
    int MediaType
);

public record CreateInventoryItemRequest(
    Guid BranchId,
    Guid StockLocationId,
    string Name,
    string? ItemCode,
    string? TagNumber,
    string? Barcode,
    Guid MaterialId,
    Guid CategoryId,
    Guid PurityId,
    decimal GrossWeight,
    decimal StoneWeight,
    int PieceCount = 1,
    int MakingChargeMethod = 1,
    decimal MakingChargeValue = 0.00m,
    string? Description = null,
    bool IsActive = true,
    List<CreateInventoryItemSpecificationRequest>? Specifications = null,
    List<CreateInventoryItemMediaRequest>? Media = null
);

public record CreateInventoryItemSpecificationRequest(
    Guid SpecificationId,
    Guid? SpecificationOptionId,
    string? CustomValue
);

public record UpdateInventoryItemRequest(
    Guid StockLocationId,
    string Name,
    string? ItemCode,
    Guid MaterialId,
    Guid CategoryId,
    Guid PurityId,
    decimal GrossWeight,
    decimal StoneWeight,
    int PieceCount,
    int MakingChargeMethod,
    decimal MakingChargeValue,
    string? Description,
    bool IsActive,
    List<CreateInventoryItemSpecificationRequest>? Specifications = null,
    List<CreateInventoryItemMediaRequest>? Media = null,
    uint? Version = null
);

public record AdminStatusCorrectionRequest(
    int TargetStatus,
    string Reason
);

public record InventoryLookupDto(
    Guid Id,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    Guid StockLocationId,
    string LocationCode,
    string LocationName,
    string TagNumber,
    string Barcode,
    string Name,
    string MaterialName,
    string CategoryName,
    decimal KaratValue,
    decimal GrossWeight,
    decimal StoneWeight,
    decimal NetWeight,
    int PieceCount,
    int Status,
    string StatusName,
    int MakingChargeMethod,
    decimal MakingChargeValue
);

public record InventoryQueryParams(
    string? Search = null,
    Guid? BranchId = null,
    Guid? StockLocationId = null,
    Guid? MaterialId = null,
    Guid? CategoryId = null,
    Guid? PurityId = null,
    int? Status = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20
);

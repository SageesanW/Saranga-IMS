namespace Saranga.Application.DTOs.Inventory;

public record BulkImportRowDto(
    int RowNumber,
    string ItemName,
    string? ItemCode,
    string MaterialCode,
    string CategoryCode,
    string PurityCode,
    decimal GrossWeight,
    decimal StoneWeight,
    decimal NetWeight,
    int PieceCount,
    string StockLocationCode,
    string MakingChargeMethod,
    decimal MakingChargeValue,
    string? Description,
    bool IsValid,
    List<string> Errors,
    Guid? MaterialId = null,
    Guid? CategoryId = null,
    Guid? PurityId = null,
    Guid? StockLocationId = null,
    int MakingChargeMethodValue = 1
);

public record BulkImportErrorDto(
    int RowNumber,
    string Field,
    string Message
);

public record BulkImportValidationResultDto(
    Guid BatchId,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    string OriginalFileName,
    string FileType,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    bool IsAllValid,
    List<BulkImportRowDto> Rows,
    List<BulkImportErrorDto> Errors
);

public record ConfirmBulkImportRequest(
    Guid BatchId
);

public record ImportedItemSummaryDto(
    Guid Id,
    string ItemName,
    string TagNumber,
    string Barcode,
    decimal GrossWeight,
    decimal NetWeight,
    string StockLocationCode
);

public record BulkImportResultDto(
    Guid BatchId,
    Guid BranchId,
    string Status,
    int TotalImported,
    DateTimeOffset ConfirmedAt,
    List<ImportedItemSummaryDto> CreatedItems
);

public record InventoryImportBatchDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string OriginalFileName,
    string FileType,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int Status,
    string StatusName,
    Guid ImportedBy,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset CreatedAt
);

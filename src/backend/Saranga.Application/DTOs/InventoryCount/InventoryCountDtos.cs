using Saranga.Domain.Enums;

namespace Saranga.Application.DTOs.InventoryCount;

public record InventoryCountDto(
    Guid Id,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    Guid StockLocationId,
    string StockLocationCode,
    string StockLocationName,
    int Status,
    string StatusName,
    DateTimeOffset StartedAt,
    Guid StartedBy,
    string? StartedByName,
    DateTimeOffset? ReconciledAt,
    Guid? ReconciledBy,
    string? ReconciledByName,
    DateTimeOffset? CompletedAt,
    Guid? CompletedBy,
    string? CompletedByName,
    DateTimeOffset? CancelledAt,
    Guid? CancelledBy,
    string? CancellationReason,
    int ExpectedItemCount,
    int CountedExpectedItemCount,
    int MatchedCount,
    int MissingCount,
    int UnexpectedCount,
    decimal ProgressPercentage,
    string? Notes,
    uint Version
);

public record InventoryCountLineDto(
    Guid Id,
    Guid InventoryCountId,
    Guid InventoryItemId,
    Guid ExpectedBranchId,
    Guid ExpectedStockLocationId,
    string TagNumberSnapshot,
    string BarcodeSnapshot,
    string ItemNameSnapshot,
    string MaterialNameSnapshot,
    string CategoryNameSnapshot,
    string PurityNameSnapshot,
    decimal GrossWeightSnapshot,
    decimal StoneWeightSnapshot,
    decimal NetWeightSnapshot,
    int PieceCountSnapshot,
    string ExpectedStatusSnapshot,
    bool IsExpected,
    bool IsCounted,
    DateTimeOffset? CountedAt,
    Guid? CountedBy,
    int Result,
    string ResultName,
    int ResolutionStatus,
    string ResolutionStatusName,
    string? ResolutionReasonCode,
    string? ResolutionNotes,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedBy
);

public record InventoryCountUnexpectedItemDto(
    Guid Id,
    Guid InventoryCountId,
    Guid InventoryItemId,
    Guid ObservedBranchId,
    Guid ObservedStockLocationId,
    Guid CurrentSystemStockLocationId,
    string CurrentSystemLocationNameSnapshot,
    string TagNumberSnapshot,
    string BarcodeSnapshot,
    string ItemNameSnapshot,
    string MaterialNameSnapshot,
    string CategoryNameSnapshot,
    string PurityNameSnapshot,
    decimal GrossWeightSnapshot,
    decimal StoneWeightSnapshot,
    decimal NetWeightSnapshot,
    int PieceCountSnapshot,
    string CurrentSystemStatusSnapshot,
    DateTimeOffset ScannedAt,
    Guid ScannedBy,
    int ResolutionStatus,
    string ResolutionStatusName,
    string? ResolutionReasonCode,
    string? ResolutionNotes,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedBy
);

public record StartInventoryCountRequest(
    Guid BranchId,
    Guid StockLocationId,
    string? Notes
);

public record ScanInventoryItemRequest(
    string Identifier
);

public record ScanResultDto(
    string ScanOutcome, // "MATCHED", "UNEXPECTED", "ALREADY_COUNTED", "COMPOSITION_PARENT", "CROSS_BRANCH", "NOT_FOUND"
    string Message,
    InventoryCountLineDto? ExpectedLine,
    InventoryCountUnexpectedItemDto? UnexpectedItem,
    CompositionScanPreviewDto? CompositionPreview,
    InventoryCountDto CountSummary
);

public record CompositionScanPreviewDto(
    Guid CompositionId,
    string Name,
    string TagNumber,
    string Barcode,
    string Type,
    List<CompositionMemberScanPreviewDto> Members
);

public record CompositionMemberScanPreviewDto(
    Guid InventoryItemId,
    string TagNumber,
    string Barcode,
    string ItemName,
    string CategoryName,
    decimal PurityKarat,
    decimal GrossWeight,
    bool IsAlreadyCounted,
    bool IsExpectedInBaseline
);

public record ConfirmCompositionScanRequest(
    Guid CompositionId,
    List<Guid> SelectedInventoryItemIds
);

public record ConfirmCompositionScanResultDto(
    int NewlyCountedCount,
    int AlreadyCountedCount,
    int UnexpectedCount,
    string Message,
    InventoryCountDto CountSummary
);

public record ResolveVarianceRequest(
    string VarianceType, // "Missing" or "Unexpected"
    Guid VarianceId,
    string ReasonCode,
    string? Notes
);

public record CompleteInventoryCountRequest(
    string? Notes
);

public record CancelInventoryCountRequest(
    string Reason
);

public record InventoryCountQueryParams(
    Guid? BranchId = null,
    Guid? StockLocationId = null,
    InventoryCountStatus? Status = null,
    int Page = 1,
    int PageSize = 20
);

public record InventoryCountActiveCheckDto(
    bool HasActiveCount,
    Guid? ActiveCountId,
    int? Status,
    string? StatusName,
    DateTimeOffset? StartedAt
);

public record CountVariancesDto(
    Guid CountId,
    int MissingCount,
    int UnexpectedCount,
    List<InventoryCountLineDto> MissingItems,
    List<InventoryCountUnexpectedItemDto> UnexpectedItems
);

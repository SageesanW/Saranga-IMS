using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InventoryImportBatch : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // "XLSX" or "CSV"

    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }

    public ImportBatchStatus Status { get; set; } = ImportBatchStatus.Uploaded;

    public Guid ImportedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Parsed payload JSON stored during validation so that the confirmed import
    /// executes the exact validated records transactionally without needing re-upload.
    /// </summary>
    public string? PayloadJson { get; set; }

    /// <summary>
    /// Stored validation errors for inspection and reporting.
    /// </summary>
    public string? ValidationErrorsJson { get; set; }

    /// <summary>
    /// Stored import summary results (created item IDs, tags, barcodes).
    /// </summary>
    public string? ResultSummaryJson { get; set; }

    public bool IsActive { get; set; } = true;
}

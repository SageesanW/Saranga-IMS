using Saranga.Application.DTOs.Inventory;

namespace Saranga.Application.Common.Interfaces;

public interface IBulkImportService
{
    Task<(byte[] Content, string ContentType, string FileName)> GenerateTemplateAsync(string format, CancellationToken cancellationToken = default);
    Task<BulkImportValidationResultDto> ValidateImportFileAsync(Guid branchId, Stream stream, string fileName, CancellationToken cancellationToken = default);
    Task<BulkImportValidationResultDto> GetBatchValidationAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<BulkImportResultDto> ConfirmImportAsync(Guid batchId, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<List<InventoryImportBatchDto>> GetBatchesAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
}

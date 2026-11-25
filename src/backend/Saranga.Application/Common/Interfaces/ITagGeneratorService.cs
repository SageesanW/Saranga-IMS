using Saranga.Domain.Enums;

namespace Saranga.Application.Common.Interfaces;

public interface ITagGeneratorService
{
    Task<(string TagNumber, string Barcode)> GenerateTagAndBarcodeAsync(string branchCode, string categoryOrMaterialCode, CancellationToken cancellationToken = default);
    Task<(string TagNumber, string Barcode)> GenerateCompositionTagAndBarcodeAsync(string branchCode, CompositionType compositionType, CancellationToken cancellationToken = default);
    Task<string> GenerateCustomerCodeAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateShiftNumberAsync(string branchCode, CancellationToken cancellationToken = default);
    Task<string> GenerateCashLedgerReferenceAsync(string branchCode, CancellationToken cancellationToken = default);
}

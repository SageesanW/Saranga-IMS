namespace Saranga.Application.Common.Interfaces;

public interface ITagGeneratorService
{
    Task<(string TagNumber, string Barcode)> GenerateTagAndBarcodeAsync(string branchCode, string categoryOrMaterialCode, CancellationToken cancellationToken = default);
}

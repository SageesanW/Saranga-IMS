using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Interfaces;
using Saranga.Infrastructure.Persistence;

namespace Saranga.Infrastructure.Services;

public class TagGeneratorService : ITagGeneratorService
{
    private readonly AppDbContext _context;

    public TagGeneratorService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(string TagNumber, string Barcode)> GenerateTagAndBarcodeAsync(string branchCode, string categoryOrMaterialCode, CancellationToken cancellationToken = default)
    {
        long seqValue;
        try
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT nextval('\"InventoryTagSequence\"');";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            seqValue = Convert.ToInt64(result);
        }
        catch
        {
            // Thread-safe fallback for mock/disconnected environments
            seqValue = Random.Shared.NextInt64(100001, 999999);
        }

        var cleanBranch = string.IsNullOrWhiteSpace(branchCode) ? "BR" : branchCode.Trim().ToUpperInvariant();
        var cleanCat = string.IsNullOrWhiteSpace(categoryOrMaterialCode) ? "JWL" : categoryOrMaterialCode.Trim().ToUpperInvariant();

        var tagNumber = $"{cleanBranch}-{cleanCat}-{seqValue:D6}";
        var barcode = $"SRG{cleanBranch}{seqValue:D8}";

        return (tagNumber, barcode);
    }
}

namespace Saranga.Application.Common.Interfaces;

public record ProviderNormalizedRate(
    string MetalSymbol, // e.g. "XAU" (Gold), "XAG" (Silver)
    decimal RatePerGram24K,
    Dictionary<decimal, decimal>? KaratRates = null // Karat -> Rate/g if provided directly
);

public record MetalRateProviderResult(
    bool IsSuccess,
    string ProviderName,
    DateTimeOffset Timestamp,
    List<ProviderNormalizedRate> NormalizedRates,
    string? ErrorMessage = null
);

public interface IMetalRateProvider
{
    string ProviderName { get; }
    Task<MetalRateProviderResult> FetchRatesAsync(string currencyCode, CancellationToken cancellationToken = default);
}

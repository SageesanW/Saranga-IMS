namespace Saranga.Application.Common.Interfaces;

public record ProviderNormalizedRate(
    string MetalSymbol, // e.g. "XAU" (Gold), "XAG" (Silver)
    decimal RatePerGram24K,
    Dictionary<decimal, decimal>? KaratRates = null, // Karat -> Rate/g if provided directly
    decimal? RatePerPawn24K = null,
    decimal? RatePerPawn22K = null
);

public record GoldPriceSriLankaFeed(
    bool IsSuccess,
    string ProviderName,
    string Currency,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? MarketTime,
    string? CbslDate,
    ProviderNormalizedRate? MarketGold,
    ProviderNormalizedRate? CbslGold,
    string AttributionText,
    string AttributionUrl,
    string? ErrorMessage = null
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
    Task<GoldPriceSriLankaFeed> FetchSriLankaFeedAsync(CancellationToken cancellationToken = default);
}

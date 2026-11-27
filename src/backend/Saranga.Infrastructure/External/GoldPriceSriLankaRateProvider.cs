using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Interfaces;

namespace Saranga.Infrastructure.External;

public class GoldPriceSriLankaRateProvider : IMetalRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoldPriceSriLankaRateProvider> _logger;

    // In-memory response cache and concurrency gate
    private static GoldPriceSriLankaFeed? _cachedFeed;
    private static DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim _fetchLock = new(1, 1);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5); // 5-minute cache matching provider TTL

    public string ProviderName => _configuration["GoldApi:ProviderName"] ?? "GoldPriceSriLanka.com";

    public GoldPriceSriLankaRateProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoldPriceSriLankaRateProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MetalRateProviderResult> FetchRatesAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        var targetCurrency = string.IsNullOrWhiteSpace(currencyCode) ? "LKR" : currencyCode.Trim().ToUpperInvariant();
        if (targetCurrency != "LKR")
        {
            return new MetalRateProviderResult(
                IsSuccess: false,
                ProviderName: ProviderName,
                Timestamp: DateTimeOffset.UtcNow,
                NormalizedRates: new List<ProviderNormalizedRate>(),
                ErrorMessage: $"Provider {ProviderName} provides Sri Lankan rates in LKR only. Requested currency: {targetCurrency}."
            );
        }

        var feed = await FetchSriLankaFeedAsync(cancellationToken);
        if (!feed.IsSuccess || feed.MarketGold == null)
        {
            return new MetalRateProviderResult(
                IsSuccess: false,
                ProviderName: ProviderName,
                Timestamp: DateTimeOffset.UtcNow,
                NormalizedRates: new List<ProviderNormalizedRate>(),
                ErrorMessage: feed.ErrorMessage ?? "Unable to retrieve valid market gold rates from provider."
            );
        }

        return new MetalRateProviderResult(
            IsSuccess: true,
            ProviderName: ProviderName,
            Timestamp: feed.MarketTime ?? feed.UpdatedAt,
            NormalizedRates: new List<ProviderNormalizedRate> { feed.MarketGold }
        );
    }

    public async Task<GoldPriceSriLankaFeed> FetchSriLankaFeedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Fast path: return fresh in-memory cached feed
        if (_cachedFeed != null && _cachedFeed.IsSuccess && now < _cacheExpiresAt)
        {
            return _cachedFeed;
        }

        // 2. Prevent duplicate concurrent requests with single-flight lock
        await _fetchLock.WaitAsync(cancellationToken);
        try
        {
            // Double check after acquiring lock
            now = DateTimeOffset.UtcNow;
            if (_cachedFeed != null && _cachedFeed.IsSuccess && now < _cacheExpiresAt)
            {
                return _cachedFeed;
            }

            var requestUri = _configuration["GoldApi:BaseUrl"] ?? "https://goldpricesrilanka.com/wp-json/gpsl/v1/price";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("User-Agent", "SarangaJewellerySystem/1.0 (+https://goldpricesrilanka.com)");
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("{Provider} returned HTTP {StatusCode}. Body: {Body}",
                    ProviderName, (int)response.StatusCode, errorBody.Length > 200 ? errorBody[..200] : errorBody);

                // If we have an existing cached feed, return it rather than hard failing
                if (_cachedFeed != null && _cachedFeed.IsSuccess)
                {
                    _logger.LogInformation("Returning previous cached gold feed due to upstream HTTP failure.");
                    return _cachedFeed;
                }

                return new GoldPriceSriLankaFeed(
                    IsSuccess: false,
                    ProviderName: ProviderName,
                    Currency: "LKR",
                    UpdatedAt: now,
                    MarketTime: null,
                    CbslDate: null,
                    MarketGold: null,
                    CbslGold: null,
                    AttributionText: "Gold prices by GoldPriceSriLanka.com",
                    AttributionUrl: "https://goldpricesrilanka.com/",
                    ErrorMessage: $"Upstream gold provider ({ProviderName}) returned HTTP {(int)response.StatusCode}."
                );
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;

            if (root.TryGetProperty("ok", out var okProp) && !okProp.GetBoolean())
            {
                return new GoldPriceSriLankaFeed(
                    IsSuccess: false,
                    ProviderName: ProviderName,
                    Currency: "LKR",
                    UpdatedAt: now,
                    MarketTime: null,
                    CbslDate: null,
                    MarketGold: null,
                    CbslGold: null,
                    AttributionText: "Gold prices by GoldPriceSriLanka.com",
                    AttributionUrl: "https://goldpricesrilanka.com/",
                    ErrorMessage: "Provider reported ok: false."
                );
            }

            var currency = root.TryGetProperty("currency", out var currProp) ? currProp.GetString() ?? "LKR" : "LKR";
            var updatedStr = root.TryGetProperty("updated", out var updProp) ? updProp.GetString() : null;
            var updatedAt = DateTimeOffset.TryParse(updatedStr, out var parsedUpd) ? parsedUpd : now;

            // Extract Market rates
            ProviderNormalizedRate? marketGold = null;
            DateTimeOffset? marketTime = null;
            if (root.TryGetProperty("market", out var marketElem))
            {
                if (marketElem.TryGetProperty("time", out var timeProp) && DateTimeOffset.TryParse(timeProp.GetString(), out var parsedTime))
                {
                    marketTime = parsedTime;
                }

                if (marketElem.TryGetProperty("karats", out var karatsElem))
                {
                    marketGold = ParseKarats(karatsElem);
                }
            }

            // Extract CBSL rates
            ProviderNormalizedRate? cbslGold = null;
            string? cbslDate = null;
            if (root.TryGetProperty("cbsl", out var cbslElem))
            {
                if (cbslElem.TryGetProperty("date", out var dateProp))
                {
                    cbslDate = dateProp.GetString();
                }

                if (cbslElem.TryGetProperty("karats", out var karatsElem))
                {
                    cbslGold = ParseKarats(karatsElem);
                }
            }

            // Extract Attribution
            var attrText = "Gold prices by GoldPriceSriLanka.com";
            var attrUrl = "https://goldpricesrilanka.com/";
            if (root.TryGetProperty("attribution", out var attrElem))
            {
                if (attrElem.TryGetProperty("text", out var textProp) && !string.IsNullOrWhiteSpace(textProp.GetString()))
                {
                    attrText = textProp.GetString()!;
                }
                if (attrElem.TryGetProperty("url", out var urlProp) && !string.IsNullOrWhiteSpace(urlProp.GetString()))
                {
                    attrUrl = urlProp.GetString()!;
                }
            }

            // Validate that we got at least 24K and 22K rates
            if (marketGold == null || marketGold.RatePerGram24K <= 0m)
            {
                return new GoldPriceSriLankaFeed(
                    IsSuccess: false,
                    ProviderName: ProviderName,
                    Currency: currency,
                    UpdatedAt: updatedAt,
                    MarketTime: marketTime,
                    CbslDate: cbslDate,
                    MarketGold: null,
                    CbslGold: cbslGold,
                    AttributionText: attrText,
                    AttributionUrl: attrUrl,
                    ErrorMessage: "Provider returned invalid or missing market 24K gold rates."
                );
            }

            var feed = new GoldPriceSriLankaFeed(
                IsSuccess: true,
                ProviderName: ProviderName,
                Currency: currency,
                UpdatedAt: updatedAt,
                MarketTime: marketTime,
                CbslDate: cbslDate,
                MarketGold: marketGold,
                CbslGold: cbslGold,
                AttributionText: attrText,
                AttributionUrl: attrUrl
            );

            // Store in cache
            _cachedFeed = feed;
            _cacheExpiresAt = now.Add(CacheDuration);

            _logger.LogInformation("Successfully refreshed gold rates from {Provider}. 24K Market: {Rate24K} LKR/g, 22K Market: {Rate22K} LKR/g",
                ProviderName, marketGold.RatePerGram24K, marketGold.KaratRates?.GetValueOrDefault(22.0m));

            return feed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching gold prices from {Provider}", ProviderName);

            // Return cached feed if available during network transient failures
            if (_cachedFeed != null && _cachedFeed.IsSuccess)
            {
                _logger.LogInformation("Returning previous cached gold feed following exception.");
                return _cachedFeed;
            }

            return new GoldPriceSriLankaFeed(
                IsSuccess: false,
                ProviderName: ProviderName,
                Currency: "LKR",
                UpdatedAt: DateTimeOffset.UtcNow,
                MarketTime: null,
                CbslDate: null,
                MarketGold: null,
                CbslGold: null,
                AttributionText: "Gold prices by GoldPriceSriLanka.com",
                AttributionUrl: "https://goldpricesrilanka.com/",
                ErrorMessage: $"Provider connection failure: {ex.Message}"
            );
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private static ProviderNormalizedRate? ParseKarats(JsonElement karatsElem)
    {
        var karatRates = new Dictionary<decimal, decimal>();
        decimal rate24k = 0m;
        decimal? pawn24k = null;
        decimal? pawn22k = null;

        if (karatsElem.TryGetProperty("24K", out var k24))
        {
            if (k24.TryGetProperty("gram", out var g24)) rate24k = g24.GetDecimal();
            if (k24.TryGetProperty("pawn", out var p24)) pawn24k = p24.GetDecimal();
            karatRates[24.0m] = rate24k;
        }

        if (karatsElem.TryGetProperty("22K", out var k22))
        {
            if (k22.TryGetProperty("gram", out var g22)) karatRates[22.0m] = g22.GetDecimal();
            if (k22.TryGetProperty("pawn", out var p22)) pawn22k = p22.GetDecimal();
        }

        if (karatsElem.TryGetProperty("21K", out var k21))
        {
            if (k21.TryGetProperty("gram", out var g21)) karatRates[21.0m] = g21.GetDecimal();
        }

        if (karatsElem.TryGetProperty("18K", out var k18))
        {
            if (k18.TryGetProperty("gram", out var g18)) karatRates[18.0m] = g18.GetDecimal();
        }

        if (rate24k <= 0m) return null;

        return new ProviderNormalizedRate(
            MetalSymbol: "XAU",
            RatePerGram24K: rate24k,
            KaratRates: karatRates,
            RatePerPawn24K: pawn24k,
            RatePerPawn22K: pawn22k
        );
    }
}

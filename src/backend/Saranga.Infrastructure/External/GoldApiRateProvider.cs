using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Interfaces;

namespace Saranga.Infrastructure.External;

public class GoldApiRateProvider : IMetalRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoldApiRateProvider> _logger;

    public string ProviderName => "GoldApi";

    public GoldApiRateProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GoldApiRateProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MetalRateProviderResult> FetchRatesAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["GoldApi:ApiKey"] ?? Environment.GetEnvironmentVariable("GOLD_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GoldApi rate refresh requested but no API key is configured in GoldApi:ApiKey or GOLD_API_KEY environment variable.");
            return new MetalRateProviderResult(
                IsSuccess: false,
                ProviderName: ProviderName,
                Timestamp: DateTimeOffset.UtcNow,
                NormalizedRates: new List<ProviderNormalizedRate>(),
                ErrorMessage: "External gold provider API key is not configured. Manual rate management remains fully operational."
            );
        }

        try
        {
            var targetCurrency = string.IsNullOrWhiteSpace(currencyCode) ? "LKR" : currencyCode.ToUpperInvariant();
            var requestUri = $"https://www.goldapi.io/api/XAU/{targetCurrency}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("x-access-token", apiKey);
            request.Headers.Add("User-Agent", "SarangaJewellerySystem/1.0");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("GoldApi returned non-success HTTP {StatusCode}. Body summary: {Summary}",
                    (int)response.StatusCode, errorBody.Length > 200 ? errorBody[..200] : errorBody);

                return new MetalRateProviderResult(
                    IsSuccess: false,
                    ProviderName: ProviderName,
                    Timestamp: DateTimeOffset.UtcNow,
                    NormalizedRates: new List<ProviderNormalizedRate>(),
                    ErrorMessage: $"External gold provider returned HTTP {(int)response.StatusCode}. Existing store rates retained."
                );
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = doc.RootElement;

            // GoldApi returns price_gram_24k, price_gram_22k, price_gram_21k, price_gram_20k, price_gram_18k, etc.
            if (!root.TryGetProperty("price_gram_24k", out var price24kProp))
            {
                return new MetalRateProviderResult(
                    IsSuccess: false,
                    ProviderName: ProviderName,
                    Timestamp: DateTimeOffset.UtcNow,
                    NormalizedRates: new List<ProviderNormalizedRate>(),
                    ErrorMessage: "Malformed response from external provider: missing 'price_gram_24k'."
                );
            }

            var rate24k = price24kProp.GetDecimal();
            var karatRates = new Dictionary<decimal, decimal>
            {
                [24.0m] = rate24k
            };

            if (root.TryGetProperty("price_gram_22k", out var p22k)) karatRates[22.0m] = p22k.GetDecimal();
            if (root.TryGetProperty("price_gram_21k", out var p21k)) karatRates[21.0m] = p21k.GetDecimal();
            if (root.TryGetProperty("price_gram_20k", out var p20k)) karatRates[20.0m] = p20k.GetDecimal();
            if (root.TryGetProperty("price_gram_18k", out var p18k)) karatRates[18.0m] = p18k.GetDecimal();

            var normalizedGold = new ProviderNormalizedRate("XAU", rate24k, karatRates);

            return new MetalRateProviderResult(
                IsSuccess: true,
                ProviderName: ProviderName,
                Timestamp: DateTimeOffset.UtcNow,
                NormalizedRates: new List<ProviderNormalizedRate> { normalizedGold }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during external gold rate fetch from {Provider}", ProviderName);
            return new MetalRateProviderResult(
                IsSuccess: false,
                ProviderName: ProviderName,
                Timestamp: DateTimeOffset.UtcNow,
                NormalizedRates: new List<ProviderNormalizedRate>(),
                ErrorMessage: $"Provider connection failure: {ex.Message}. Existing store rates retained."
            );
        }
    }
}

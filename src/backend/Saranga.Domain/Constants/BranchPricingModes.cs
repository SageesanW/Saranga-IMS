namespace Saranga.Domain.Constants;

public static class BranchPricingModes
{
    public const string Manual = "MANUAL";
    public const string MarketApi = "MARKET_API";
    public const string CbslApi = "CBSL_API";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Manual,
        MarketApi,
        CbslApi
    };
}

namespace Saranga.Domain.Constants;

public static class MetalRateSources
{
    public const string Manual = "MANUAL";
    public const string ExternalApi = "EXTERNAL_API";
    public const string MarketApi = "MARKET_API";
    public const string CbslApi = "CBSL_API";
    public const string AdminOverride = "ADMIN_OVERRIDE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Manual,
        ExternalApi,
        MarketApi,
        CbslApi,
        AdminOverride
    };
}

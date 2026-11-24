namespace Saranga.Domain.Constants;

public static class StockLocationTypes
{
    public const string Display = "DISPLAY";
    public const string Vault = "VAULT";
    public const string Workshop = "WORKSHOP";
    public const string Transit = "TRANSIT";
    public const string Storage = "STORAGE";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Display,
        Vault,
        Workshop,
        Transit,
        Storage
    };

    public static string Normalize(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return Display;
        var trimmed = type.Trim().ToUpperInvariant();
        if (All.Contains(trimmed)) return trimmed;

        throw new ArgumentException($"Invalid location type '{type}'. Allowed types are: {string.Join(", ", All)}.");
    }
}

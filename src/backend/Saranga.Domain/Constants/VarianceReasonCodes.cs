namespace Saranga.Domain.Constants;

public static class VarianceReasonCodes
{
    public const string ItemFound = "ITEM_FOUND";
    public const string WrongRecordedLocation = "WRONG_RECORDED_LOCATION";
    public const string ConfirmedMissing = "CONFIRMED_MISSING";
    public const string InventoryRecordCorrection = "INVENTORY_RECORD_CORRECTION";
    public const string Other = "OTHER";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ItemFound,
        WrongRecordedLocation,
        ConfirmedMissing,
        InventoryRecordCorrection,
        Other
    };

    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var normalized = code.Trim().ToUpperInvariant();
        return All.Contains(normalized);
    }
}

namespace Saranga.Domain.Constants;

public static class VatCalculationModes
{
    public const string FlatPercentage = "FLAT_PERCENTAGE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        FlatPercentage
    };
}

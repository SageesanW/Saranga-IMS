namespace Saranga.Domain.Constants;

public static class SpecificationInputTypes
{
    public const string Predefined = "PREDEFINED";
    public const string FreeText = "FREE_TEXT";
    public const string Hybrid = "HYBRID";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Predefined,
        FreeText,
        Hybrid
    };

    public static bool IsValid(string? inputType) =>
        !string.IsNullOrWhiteSpace(inputType) && All.Contains(inputType.Trim().ToUpperInvariant());
}

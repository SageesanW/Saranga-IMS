namespace Saranga.Domain.Enums;

public enum MakingChargeMethod
{
    PerGram = 1,
    PerItem = 2
}

public record MakingChargeMethodDefinition(
    int Id,
    string Name,
    string Code,
    string Description,
    bool IsDefault
);

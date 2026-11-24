namespace Saranga.Application.DTOs.Branches;

public record BranchDto(
    Guid Id,
    string Name,
    string Code,
    string Country,
    string CurrencyCode,
    string TimeZone,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record CreateBranchRequest(
    string Name,
    string Code,
    string Country,
    string CurrencyCode,
    string TimeZone,
    bool IsActive = true
);

public record UpdateBranchRequest(
    string Name,
    string Country,
    string CurrencyCode,
    string TimeZone,
    bool IsActive
);

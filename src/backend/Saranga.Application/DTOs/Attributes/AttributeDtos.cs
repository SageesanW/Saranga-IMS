namespace Saranga.Application.DTOs.Attributes;

public record MaterialDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAt
);

public record CreateMaterialRequest(
    string Name,
    string Code,
    string Description,
    bool IsActive = true,
    int SortOrder = 0
);

public record UpdateMaterialRequest(
    string Name,
    string Description,
    bool IsActive,
    int SortOrder
);

public record CategoryDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAt
);

public record CreateCategoryRequest(
    string Name,
    string Code,
    string Description,
    bool IsActive = true,
    int SortOrder = 0
);

public record UpdateCategoryRequest(
    string Name,
    string Description,
    bool IsActive,
    int SortOrder
);

public record PurityDto(
    Guid Id,
    string Name,
    string Code,
    decimal KaratValue,
    decimal PurityPercentage,
    string Description,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAt
);

public record CreatePurityRequest(
    string Name,
    string Code,
    decimal KaratValue,
    decimal PurityPercentage,
    string Description,
    bool IsActive = true,
    int SortOrder = 0
);

public record UpdatePurityRequest(
    string Name,
    decimal KaratValue,
    decimal PurityPercentage,
    string Description,
    bool IsActive,
    int SortOrder
);

public record SpecificationOptionDto(
    Guid Id,
    Guid SpecificationId,
    string Value,
    string Code,
    string Description,
    bool IsActive,
    int SortOrder
);

public record SpecificationDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    string InputType,
    bool IsActive,
    int SortOrder,
    List<SpecificationOptionDto> Options,
    DateTimeOffset CreatedAt
);

public record CreateSpecificationRequest(
    string Name,
    string Code,
    string Description,
    bool IsActive = true,
    int SortOrder = 0,
    string InputType = "PREDEFINED"
);

public record UpdateSpecificationRequest(
    string Name,
    string Description,
    string? InputType,
    bool IsActive,
    int SortOrder
);

public record CreateSpecificationOptionRequest(
    string Value,
    string Code,
    string Description,
    bool IsActive = true,
    int SortOrder = 0
);

public record StockLocationDto(
    Guid Id,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    string Name,
    string Code,
    string LocationType,
    string Description,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAt
);

public record CreateStockLocationRequest(
    Guid BranchId,
    string Name,
    string Code,
    string LocationType,
    string Description,
    bool IsActive = true,
    int SortOrder = 0
);

public record UpdateStockLocationRequest(
    string Name,
    string LocationType,
    string Description,
    bool IsActive,
    int SortOrder
);

public record MakingChargeMethodDto(
    int Id,
    string Name,
    string Code,
    string Description,
    bool IsDefault
);

namespace Saranga.Application.DTOs.Customers;

public record CustomerDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string PrimaryPhone,
    string NormalizedPrimaryPhone,
    string? SecondaryPhone,
    string? NormalizedSecondaryPhone,
    string? Email,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? DistrictProvince,
    string? PostalCode,
    string Country,
    DateOnly? DateOfBirth,
    string? Notes,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    string? CreatedByName,
    DateTimeOffset? UpdatedAt,
    Guid? UpdatedBy,
    string? UpdatedByName
);

public record CustomerLookupDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string PrimaryPhone,
    string? Email,
    string? City,
    bool IsActive
);

public record CreateCustomerRequest(
    string FullName,
    string PrimaryPhone,
    string? SecondaryPhone = null,
    string? Email = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? DistrictProvince = null,
    string? PostalCode = null,
    string? Country = null,
    DateOnly? DateOfBirth = null,
    string? Notes = null
);

public record UpdateCustomerRequest(
    string FullName,
    string PrimaryPhone,
    string? SecondaryPhone = null,
    string? Email = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? DistrictProvince = null,
    string? PostalCode = null,
    string? Country = null,
    DateOnly? DateOfBirth = null,
    string? Notes = null,
    uint? RowVersion = null
);

public record UpdateCustomerStatusRequest(
    bool IsActive,
    string? Reason = null
);

public record CustomerQueryParams(
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 25
);

public record CustomerDuplicateCheckResult(
    bool IsDuplicate,
    string? DuplicateReason,
    CustomerLookupDto? ExistingCustomer
);

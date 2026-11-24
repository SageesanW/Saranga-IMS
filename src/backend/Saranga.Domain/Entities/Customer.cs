using Saranga.Domain.Common;

namespace Saranga.Domain.Entities;

public class Customer : BaseEntity
{
    public string CustomerCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string PrimaryPhone { get; set; } = string.Empty;

    public string NormalizedPrimaryPhone { get; set; } = string.Empty;

    public string? SecondaryPhone { get; set; }

    public string? NormalizedSecondaryPhone { get; set; }

    public string? Email { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? DistrictProvince { get; set; }

    public string? PostalCode { get; set; }

    public string Country { get; set; } = "Sri Lanka";

    public DateOnly? DateOfBirth { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public uint Version { get; set; }

    public User? Creator { get; set; }
    public User? Updater { get; set; }
}

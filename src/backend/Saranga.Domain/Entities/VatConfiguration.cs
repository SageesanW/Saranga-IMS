namespace Saranga.Domain.Entities;

public class VatConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public string Name { get; set; } = "Standard VAT";

    public bool IsEnabled { get; set; } = false;

    public decimal RatePercentage { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EffectiveTo { get; set; }

    public string CalculationMode { get; set; } = Constants.VatCalculationModes.FlatPercentage;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? CreatedBy { get; set; }
    public User? Creator { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
    public User? Updater { get; set; }
}

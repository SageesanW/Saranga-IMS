namespace Saranga.Domain.Entities;

public class MetalRate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public Guid MaterialId { get; set; }
    public Material Material { get; set; } = null!;

    public Guid PurityId { get; set; }
    public Purity Purity { get; set; } = null!;

    public string CurrencyCode { get; set; } = "LKR";

    public decimal RatePerGram { get; set; }

    public string Source { get; set; } = Constants.MetalRateSources.Manual;

    public decimal? ExternalReferenceRate { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsOverride { get; set; } = false;

    public string? OverrideReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? CreatedBy { get; set; }
    public User? Creator { get; set; }
}

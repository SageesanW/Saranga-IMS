using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InvoiceLine : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public InvoiceLineType LineType { get; set; } = InvoiceLineType.InventoryItem;
    public Guid? InventoryItemId { get; set; }
    public Guid? CompositionId { get; set; }

    public string ItemCode { get; set; } = string.Empty;
    public string TagNumber { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string ItemName { get; set; } = string.Empty;

    public decimal GrossWeight { get; set; }
    public decimal StoneWeight { get; set; }
    public decimal NetWeight { get; set; }
    public int PieceCount { get; set; } = 1;

    public Guid? MaterialId { get; set; }
    public string? MaterialNameSnapshot { get; set; }
    public Guid? PurityId { get; set; }
    public decimal? KaratValueSnapshot { get; set; }

    public decimal ReferenceRatePerGram { get; set; }
    public decimal ReferenceMaterialValue { get; set; }
    public decimal ReferenceMakingCharge { get; set; }
    public decimal ReferencePrice { get; set; }

    // Module 13: Rate Snapshot and Pricing Details
    public Guid? MetalRateId { get; set; }
    public string? RateSource { get; set; }
    public DateTimeOffset? RateEffectiveAt { get; set; }
    public DateTimeOffset? QuotedAt { get; set; }
    public MakingChargeMethod MakingChargeMethodSnapshot { get; set; } = MakingChargeMethod.PerGram;
    public decimal MakingChargeValueSnapshot { get; set; }
    public decimal AllocatedInvoiceAdjustment { get; set; }
    public decimal NetLineTotal { get; set; }

    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }

    // Navigations
    public Invoice? Invoice { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    public InventoryComposition? Composition { get; set; }
}

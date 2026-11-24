using Saranga.Domain.Common;
using Saranga.Domain.Enums;

namespace Saranga.Domain.Entities;

public class InventoryItemMedia : BaseEntity
{
    public Guid InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public InventoryMediaType MediaType { get; set; } = InventoryMediaType.Image;
    public int SortOrder { get; set; } = 0;
    public bool IsPrimary { get; set; } = false;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

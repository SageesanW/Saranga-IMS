namespace Saranga.Domain.Enums;

public enum InventoryCountStatus
{
    InProgress = 1,
    RequiresReview = 2,
    Completed = 3,
    Cancelled = 4
}

public enum CountLineResult
{
    Pending = 0,
    Matched = 1,
    Missing = 2
}

public enum VarianceResolutionStatus
{
    Unresolved = 0,
    Resolved = 1
}

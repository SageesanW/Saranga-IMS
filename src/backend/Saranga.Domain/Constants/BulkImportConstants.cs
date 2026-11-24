namespace Saranga.Domain.Constants;

public static class BulkImportConstants
{
    public const int MaxAllowedRows = 1000;
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    public const int MaxFileSizeMb = 5;
}

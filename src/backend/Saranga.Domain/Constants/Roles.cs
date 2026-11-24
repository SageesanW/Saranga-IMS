namespace Saranga.Domain.Constants;

public static class SystemRoles
{
    public const string OwnerSuperAdmin = "Owner / Super Admin";
    public const string Manager = "Manager";
    public const string Cashier = "Cashier";
    public const string Auditor = "Auditor";

    public static readonly IReadOnlyList<string> All = new List<string>
    {
        OwnerSuperAdmin,
        Manager,
        Cashier,
        Auditor
    };

    public static bool IsSystemRole(string roleName)
    {
        return All.Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }
}

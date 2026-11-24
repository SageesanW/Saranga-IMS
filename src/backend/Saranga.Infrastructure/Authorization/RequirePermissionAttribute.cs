using Microsoft.AspNetCore.Authorization;

namespace Saranga.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "PERMISSION:";

    public RequirePermissionAttribute(params string[] permissions)
    {
        Permissions = permissions;
        Policy = $"{PolicyPrefix}{string.Join(",", permissions)}";
    }

    public string[] Permissions { get; }
}

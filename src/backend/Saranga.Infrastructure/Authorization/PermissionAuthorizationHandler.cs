using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Saranga.Domain.Constants;

namespace Saranga.Infrastructure.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Global admin (Owner / Super Admin) automatically satisfies all permissions
        var isGlobalAdmin = context.User.FindFirstValue("is_global_admin");
        if (bool.TryParse(isGlobalAdmin, out var isAdmin) && isAdmin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.IsInRole(SystemRoles.OwnerSuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if user has any of the required permission claims
        var userPerms = context.User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requirement.Permissions.Any(p => userPerms.Contains(p)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

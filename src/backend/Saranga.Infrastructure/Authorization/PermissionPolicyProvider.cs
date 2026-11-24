using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Saranga.Infrastructure.Authorization;

public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var raw = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
            var permissions = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissions))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // Also dynamically resolve direct permission names
        if (Domain.Constants.Permissions.All.Any(p => p.Name.Equals(policyName, StringComparison.OrdinalIgnoreCase)))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(new[] { policyName }))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}

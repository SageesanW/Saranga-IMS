using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Saranga.Application.Common.Interfaces;

namespace Saranga.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");

            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string? Username => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
                               ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("unique_name");

    public bool IsGlobalAdmin
    {
        get
        {
            var adminClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("is_global_admin");
            if (bool.TryParse(adminClaim, out var isAdmin) && isAdmin)
            {
                return true;
            }

            return Roles.Contains("Owner / Super Admin", StringComparer.OrdinalIgnoreCase);
        }
    }

    public IReadOnlyList<Guid> AccessibleBranchIds
    {
        get
        {
            var claims = _httpContextAccessor.HttpContext?.User?.FindAll("branch_id");
            if (claims == null) return Array.Empty<Guid>();

            var list = new List<Guid>();
            foreach (var c in claims)
            {
                if (Guid.TryParse(c.Value, out var id))
                {
                    list.Add(id);
                }
            }
            return list;
        }
    }

    public Guid? BranchId
    {
        get
        {
            // 1. Check HttpContext.Items (e.g. set by BranchContextMiddleware)
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("ActiveBranchId", out var itemVal) == true &&
                itemVal is Guid g && g != Guid.Empty)
            {
                return g;
            }

            // 2. Check X-Branch-Id header
            var headerVal = _httpContextAccessor.HttpContext?.Request?.Headers["X-Branch-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(headerVal) && Guid.TryParse(headerVal, out var headerGuid) && headerGuid != Guid.Empty)
            {
                return headerGuid;
            }

            // 3. Fallback to default_branch_id claim
            var defaultClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("default_branch_id");
            if (Guid.TryParse(defaultClaim, out var defaultGuid) && defaultGuid != Guid.Empty)
            {
                return defaultGuid;
            }

            // 4. Fallback to first accessible branch if regular user
            var accessible = AccessibleBranchIds;
            if (accessible.Count > 0 && !IsGlobalAdmin)
            {
                return accessible[0];
            }

            return null;
        }
    }

    public string? Role => Roles.FirstOrDefault();

    public IReadOnlyList<string> Roles
    {
        get
        {
            var claims = _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role);
            return claims != null ? claims.Select(c => c.Value).Distinct().ToList() : Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> Permissions
    {
        get
        {
            var claims = _httpContextAccessor.HttpContext?.User?.FindAll("permission");
            return claims != null ? claims.Select(c => c.Value).Distinct().ToList() : Array.Empty<string>();
        }
    }

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
}

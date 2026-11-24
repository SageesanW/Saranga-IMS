using Microsoft.EntityFrameworkCore;
using Saranga.Domain.Entities;

namespace Saranga.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserBranch> UserBranches { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // Module 3: Attributes Master
    DbSet<Material> Materials { get; }
    DbSet<Category> Categories { get; }
    DbSet<Purity> Purities { get; }
    DbSet<Specification> Specifications { get; }
    DbSet<SpecificationOption> SpecificationOptions { get; }
    DbSet<StockLocation> StockLocations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

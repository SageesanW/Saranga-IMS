using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Saranga.Domain.Entities;

namespace Saranga.Application.Common.Interfaces;

public interface IAppDbContext
{
    DatabaseFacade Database { get; }
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

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

    // Module 4: Inventory Core
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<InventoryItemSpecification> InventoryItemSpecifications { get; }
    DbSet<InventoryItemMedia> InventoryItemMedia { get; }

    // Module 5 Hardening: Idempotency
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    // Module 6: Assembly & Group Compositions
    DbSet<InventoryComposition> InventoryCompositions { get; }
    DbSet<InventoryCompositionMember> InventoryCompositionMembers { get; }

    // Module 7: Excel / CSV Bulk Inventory Import
    DbSet<InventoryImportBatch> InventoryImportBatches { get; }

    // Module 8: Inventory Count & Physical Stock Verification
    DbSet<InventoryCount> InventoryCounts { get; }
    DbSet<InventoryCountLine> InventoryCountLines { get; }
    DbSet<InventoryCountUnexpectedItem> InventoryCountUnexpectedItems { get; }

    // Module 9: Gold Rate Management & VAT Configuration
    DbSet<MetalRate> MetalRates { get; }
    DbSet<VatConfiguration> VatConfigurations { get; }

    // Module 10: Customer Management
    DbSet<Customer> Customers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

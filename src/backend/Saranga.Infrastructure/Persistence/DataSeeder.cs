using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Interfaces;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;

namespace Saranga.Infrastructure.Persistence;

public class DataSeeder : IDataSeeder
{
    private readonly IAppDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(IAppDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting idempotent data seeding for roles and permissions...");

        // 1. Seed Permissions
        var existingPermissions = await _context.Permissions.ToListAsync(cancellationToken);
        var existingPermNames = existingPermissions.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newPermissions = new List<Permission>();
        foreach (var def in Permissions.All)
        {
            if (!existingPermNames.Contains(def.Name))
            {
                var p = new Permission
                {
                    Name = def.Name,
                    Description = def.Description,
                    Module = def.Module,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = null
                };
                newPermissions.Add(p);
                _context.Permissions.Add(p);
            }
        }

        if (newPermissions.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} new permissions.", newPermissions.Count);
            // Refresh in-memory list
            existingPermissions = await _context.Permissions.ToListAsync(cancellationToken);
        }

        var permLookup = existingPermissions.ToDictionary(p => p.Name, p => p.Id, StringComparer.OrdinalIgnoreCase);

        // 2. Seed System Roles
        var existingRoles = await _context.Roles.ToListAsync(cancellationToken);
        var roleLookup = existingRoles.ToDictionary(r => r.Name, r => r, StringComparer.OrdinalIgnoreCase);

        var systemRoleDefinitions = new List<(string Name, string Description)>
        {
            (SystemRoles.OwnerSuperAdmin, "Unrestricted full business owner and system administrator"),
            (SystemRoles.Manager, "Branch operational manager with staff, inventory, POS, cash and verification oversight"),
            (SystemRoles.Cashier, "Front-desk sales, jewellery orders, repairs, and shift operator"),
            (SystemRoles.Auditor, "Independent compliance and system audit reviewer with read-only access")
        };

        var rolesAdded = false;
        foreach (var (roleName, description) in systemRoleDefinitions)
        {
            if (!roleLookup.TryGetValue(roleName, out var role))
            {
                role = new Role
                {
                    Name = roleName,
                    Description = description,
                    IsSystemRole = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = null
                };
                _context.Roles.Add(role);
                roleLookup[roleName] = role;
                rolesAdded = true;
            }
        }

        if (rolesAdded)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded missing system roles.");
        }

        // 3. Seed Role-Permission Mappings
        var existingRolePerms = await _context.RolePermissions.ToListAsync(cancellationToken);
        var existingRolePermSet = existingRolePerms
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .ToHashSet();

        var newRolePermissions = new List<RolePermission>();

        void AssignPermissionsToRole(string roleName, IEnumerable<string> permNames)
        {
            if (!roleLookup.TryGetValue(roleName, out var role)) return;

            foreach (var permName in permNames)
            {
                if (permLookup.TryGetValue(permName, out var permId))
                {
                    if (!existingRolePermSet.Contains((role.Id, permId)))
                    {
                        var rp = new RolePermission
                        {
                            RoleId = role.Id,
                            PermissionId = permId,
                            AssignedAt = DateTimeOffset.UtcNow
                        };
                        newRolePermissions.Add(rp);
                        existingRolePermSet.Add((role.Id, permId));
                    }
                }
            }
        }

        // Owner / Super Admin gets ALL permissions
        AssignPermissionsToRole(SystemRoles.OwnerSuperAdmin, permLookup.Keys);

        // Manager permissions
        var managerPerms = new[]
        {
            Permissions.BranchView,
            Permissions.UserView,
            Permissions.UserCreate,
            Permissions.UserEdit,
            Permissions.RoleView,
            Permissions.PermissionView,
            Permissions.AttributeView,
            Permissions.AttributeManage,
            Permissions.InventoryView,
            Permissions.InventoryCreate,
            Permissions.InventoryEdit,
            Permissions.InventoryBulkImport,
            Permissions.InventoryCountManage,
            Permissions.InventoryVarianceResolve,
            Permissions.InventoryTransfer,
            Permissions.RateView,
            Permissions.CustomerView,
            Permissions.CustomerManage,
            Permissions.PosView,
            Permissions.PosCreateSale,
            Permissions.PosPriceAdjust,
            Permissions.PosReturn,
            Permissions.OrderManage,
            Permissions.RepairManage,
            Permissions.ShiftStart,
            Permissions.ShiftEnd,
            Permissions.CashLedgerView,
            Permissions.CashLedgerAdjust,
            Permissions.SalesVerifyL1,
            Permissions.SalesFlagReview,
            Permissions.AuditView
        };
        AssignPermissionsToRole(SystemRoles.Manager, managerPerms);

        // Cashier permissions
        var cashierPerms = new[]
        {
            Permissions.AttributeView,
            Permissions.InventoryView,
            Permissions.RateView,
            Permissions.CustomerView,
            Permissions.CustomerManage,
            Permissions.PosView,
            Permissions.PosCreateSale,
            Permissions.PosPriceAdjust,
            Permissions.OrderManage,
            Permissions.RepairManage,
            Permissions.ShiftStart,
            Permissions.ShiftEnd
        };
        AssignPermissionsToRole(SystemRoles.Cashier, cashierPerms);

        // Auditor permissions (Strictly compliance, audit logs, and security governance oversight, not internal operational screens)
        var auditorPerms = new[]
        {
            Permissions.AuditView,
            Permissions.BranchView,
            Permissions.UserView,
            Permissions.RoleView,
            Permissions.PermissionView
        };
        AssignPermissionsToRole(SystemRoles.Auditor, auditorPerms);

        if (newRolePermissions.Count > 0)
        {
            _context.RolePermissions.AddRange(newRolePermissions);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Assigned {Count} new role-permission mappings.", newRolePermissions.Count);
        }

        // 4. Ensure existing Global Admins (e.g. saranga_owner from Module 1 bootstrap) have the OwnerSuperAdmin role linked
        var ownerRole = roleLookup[SystemRoles.OwnerSuperAdmin];
        var globalAdmins = await _context.Users
            .Include(u => u.UserRoles)
            .Where(u => u.IsGlobalAdmin)
            .ToListAsync(cancellationToken);

        var userRolesAdded = false;
        foreach (var admin in globalAdmins)
        {
            if (!admin.UserRoles.Any(ur => ur.RoleId == ownerRole.Id))
            {
                _context.UserRoles.Add(new UserRole
                {
                    UserId = admin.Id,
                    RoleId = ownerRole.Id,
                    AssignedAt = DateTimeOffset.UtcNow
                });
                userRolesAdded = true;
            }
        }

        if (userRolesAdded)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Linked Owner / Super Admin role to existing global admin user accounts.");
        }

        _logger.LogInformation("Idempotent data seeding completed successfully.");
    }
}

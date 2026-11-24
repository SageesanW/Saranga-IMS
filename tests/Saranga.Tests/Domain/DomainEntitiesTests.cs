using Saranga.Domain.Common;
using Saranga.Domain.Entities;
using Xunit;

namespace Saranga.Tests.Domain;

public class DomainEntitiesTests
{
    private class TestEntity : BaseEntity { }

    [Fact]
    public void BaseEntity_ShouldInitializeWithGuidAndUtcDate()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.False(entity.IsDeleted);
        Assert.True((DateTimeOffset.UtcNow - entity.CreatedAt).TotalSeconds < 5);
        Assert.Null(entity.UpdatedAt);
    }

    [Fact]
    public void RefreshToken_WhenExpiredOrRevoked_ShouldReportInactive()
    {
        var token = new RefreshToken
        {
            TokenHash = "abc123hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5) // Expired
        };

        Assert.True(token.IsExpired);
        Assert.False(token.IsActive);

        var validToken = new RefreshToken
        {
            TokenHash = "xyz789hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            RevokedAt = DateTimeOffset.UtcNow // Revoked
        };

        Assert.True(validToken.IsRevoked);
        Assert.False(validToken.IsActive);
    }

    [Fact]
    public void BranchEntity_ShouldInitializeWithDefaults()
    {
        var branch = new Branch
        {
            Name = "Jaffna Main Branch",
            Code = "JAF-01"
        };

        Assert.NotEqual(Guid.Empty, branch.Id);
        Assert.Equal("Sri Lanka", branch.Country);
        Assert.Equal("LKR", branch.CurrencyCode);
        Assert.Equal("Asia/Colombo", branch.TimeZone);
        Assert.True(branch.IsActive);
    }

    [Fact]
    public void RbacEntities_ShouldSupportUserRoleAndRolePermissionRelationships()
    {
        var branch = new Branch { Name = "Jaffna Main Branch", Code = "JAF-01" };

        var user = new User
        {
            Username = "manager_test",
            Email = "manager@saranga.lk",
            DefaultBranchId = branch.Id,
            DefaultBranch = branch
        };

        var role = new Role
        {
            Name = "Manager / Branch Admin",
            Description = "Branch oversight"
        };

        var permission = new Permission
        {
            Name = "VERIFY_LEVEL_1",
            Module = "Verification",
            Description = "Approve Level 1 Sales Verification"
        };

        var userRole = new UserRole
        {
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role
        };

        var rolePermission = new RolePermission
        {
            RoleId = role.Id,
            Role = role,
            PermissionId = permission.Id,
            Permission = permission
        };

        var userBranch = new UserBranch
        {
            UserId = user.Id,
            User = user,
            BranchId = branch.Id,
            Branch = branch,
            IsDefault = true
        };

        user.UserRoles.Add(userRole);
        role.RolePermissions.Add(rolePermission);
        user.UserBranches.Add(userBranch);

        Assert.Single(user.UserRoles);
        Assert.Single(role.RolePermissions);
        Assert.Single(user.UserBranches);
        Assert.Equal("VERIFY_LEVEL_1", role.RolePermissions.First().Permission?.Name);
        Assert.Equal(branch.Id, user.DefaultBranchId);
        Assert.Equal("JAF-01", user.UserBranches.First().Branch?.Code);
    }
}

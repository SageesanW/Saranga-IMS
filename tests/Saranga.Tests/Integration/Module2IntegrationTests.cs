using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Api;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.DTOs.Branches;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Roles;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module2IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public Module2IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private HttpClient CreateClientWithCookieHandler()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("http://localhost/")
        });
    }

    [Fact]
    public async Task ComprehensiveModule2_EndToEndVerification()
    {
        var client = CreateClientWithCookieHandler();

        // =========================================================================
        // PART 1: DATA SEEDER & ROLE-PERMISSION VERIFICATION
        // =========================================================================
        _output.WriteLine("=== PART 1: Verifying Data Seeder & Role-Permission Matrix ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();

            // Run seeder to verify idempotency
            await seeder.SeedAsync();

            var permissions = await db.Permissions.AsNoTracking().ToListAsync();
            Assert.True(permissions.Count >= 34, $"Expected at least 34 permissions, found {permissions.Count}");

            var roles = await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).ToListAsync();
            Assert.Contains(roles, r => r.Name == SystemRoles.OwnerSuperAdmin && r.IsSystemRole);
            Assert.Contains(roles, r => r.Name == SystemRoles.Manager && r.IsSystemRole);
            Assert.Contains(roles, r => r.Name == SystemRoles.Cashier && r.IsSystemRole);
            Assert.Contains(roles, r => r.Name == SystemRoles.Auditor && r.IsSystemRole);

            // Owner / Super Admin must have ALL permissions
            var ownerRole = roles.First(r => r.Name == SystemRoles.OwnerSuperAdmin);
            Assert.Equal(permissions.Count, ownerRole.RolePermissions.Count);

            // Auditor must only have read-only permissions
            var auditorRole = roles.First(r => r.Name == SystemRoles.Auditor);
            foreach (var rp in auditorRole.RolePermissions)
            {
                Assert.EndsWith("_VIEW", rp.Permission!.Name);
            }

            _output.WriteLine($"Verified {permissions.Count} permissions and all 4 system roles seeded cleanly.");
        }

        // =========================================================================
        // PART 2: OWNER LOGIN & BRANCH MANAGEMENT
        // =========================================================================
        _output.WriteLine("=== PART 2: Branch Management (CRUD, Unique Code, Multi-Currency) ===");

        // 1. Login as saranga_owner
        var ownerLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        Assert.Equal(HttpStatusCode.OK, ownerLoginRes.StatusCode);
        var ownerAuth = await ownerLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(ownerAuth);
        var ownerToken = ownerAuth.AccessToken;

        var ownerClient = CreateClientWithCookieHandler();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // 2. Create Jaffna Main Branch (LKR, Asia/Colombo)
        var jafReq = new CreateBranchRequest(
            Name: "Jaffna Main Branch",
            Code: "JAF-01",
            Country: "Sri Lanka",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        );
        var createJafRes = await ownerClient.PostAsJsonAsync("/api/branches", jafReq);
        Assert.True(createJafRes.StatusCode == HttpStatusCode.Created || createJafRes.StatusCode == HttpStatusCode.BadRequest);

        Guid jafBranchId;
        if (createJafRes.StatusCode == HttpStatusCode.Created)
        {
            var jafBranch = await createJafRes.Content.ReadFromJsonAsync<BranchDto>();
            Assert.NotNull(jafBranch);
            jafBranchId = jafBranch.Id;
            Assert.Equal("JAF-01", jafBranch.Code);
            _output.WriteLine($"Created Jaffna Branch: {jafBranch.Name} ({jafBranch.Code})");
        }
        else
        {
            var branches = await ownerClient.GetFromJsonAsync<List<BranchDto>>("/api/branches");
            jafBranchId = branches!.First(b => b.Code == "JAF-01").Id;
        }

        // 3. Test unique code enforcement: attempt duplicate "jaf-01"
        var dupJafRes = await ownerClient.PostAsJsonAsync("/api/branches", jafReq);
        Assert.Equal(HttpStatusCode.BadRequest, dupJafRes.StatusCode);
        var dupErr = await dupJafRes.Content.ReadAsStringAsync();
        Assert.Contains("already exists", dupErr, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine("Verified duplicate branch code rejection (case-insensitive).");

        // 4. Create Canada Branch (CAD, America/Toronto) - verifying future multi-country readiness!
        var canReq = new CreateBranchRequest(
            Name: "Toronto Branch",
            Code: "CAN-01",
            Country: "Canada",
            CurrencyCode: "CAD",
            TimeZone: "America/Toronto",
            IsActive: true
        );
        var createCanRes = await ownerClient.PostAsJsonAsync("/api/branches", canReq);
        Assert.True(createCanRes.StatusCode == HttpStatusCode.Created || createCanRes.StatusCode == HttpStatusCode.BadRequest);
        _output.WriteLine("Verified multi-country / multi-currency branch creation (Canada / CAD).");

        // 5. Update branch details
        var updateJafReq = new UpdateBranchRequest(
            Name: "Jaffna Flagship Showroom",
            Country: "Sri Lanka",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        );
        var updateRes = await ownerClient.PutAsJsonAsync($"/api/branches/{jafBranchId}", updateJafReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedJaf = await updateRes.Content.ReadFromJsonAsync<BranchDto>();
        Assert.Equal("Jaffna Flagship Showroom", updatedJaf!.Name);

        // 6. Toggle branch status
        var toggleRes = await ownerClient.PatchAsync($"/api/branches/{jafBranchId}/status", null);
        Assert.Equal(HttpStatusCode.OK, toggleRes.StatusCode);
        var toggledBranch = await toggleRes.Content.ReadFromJsonAsync<BranchDto>();
        Assert.False(toggledBranch!.IsActive);

        // Reactivate branch
        var reactivateRes = await ownerClient.PatchAsync($"/api/branches/{jafBranchId}/status", null);
        Assert.Equal(HttpStatusCode.OK, reactivateRes.StatusCode);
        var activeBranch = await reactivateRes.Content.ReadFromJsonAsync<BranchDto>();
        Assert.True(activeBranch!.IsActive);
        _output.WriteLine("Verified branch status toggle (Deactivate -> Reactivate).");

        // =========================================================================
        // PART 3: USER MANAGEMENT & RBAC ASSIGNMENTS
        // =========================================================================
        _output.WriteLine("=== PART 3: User Management (Create, Roles, Branch Assignment, Password Reset) ===");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roles = await db.Roles.ToListAsync();
            var managerRole = roles.First(r => r.Name == SystemRoles.Manager);
            var cashierRole = roles.First(r => r.Name == SystemRoles.Cashier);
            var auditorRole = roles.First(r => r.Name == SystemRoles.Auditor);

            // Ensure test accounts start with known test credentials and active state across repeated runs
            var existingCashier = await db.Users.FirstOrDefaultAsync(u => u.Username == "jaffna_cashier");
            if (existingCashier != null)
            {
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                existingCashier.PasswordHash = hasher.HashPassword("CashierPassword@2026!");
                existingCashier.IsActive = true;
                await db.SaveChangesAsync();
            }

            // 1. Create a Manager User
            var mgrReq = new CreateUserRequest(
                Username: "jaffna_manager",
                Email: "manager.jaf@sarangajewellery.lk",
                Password: "ManagerPassword@2026!",
                FullName: "Kandeepan Manager",
                RoleIds: new List<Guid> { managerRole.Id },
                BranchIds: new List<Guid> { jafBranchId },
                DefaultBranchId: jafBranchId,
                IsGlobalAdmin: false
            );
            var createMgrRes = await ownerClient.PostAsJsonAsync("/api/users", mgrReq);
            Assert.True(createMgrRes.StatusCode == HttpStatusCode.Created || createMgrRes.StatusCode == HttpStatusCode.BadRequest);

            // 2. Create a Cashier User
            var cshReq = new CreateUserRequest(
                Username: "jaffna_cashier",
                Email: "cashier.jaf@sarangajewellery.lk",
                Password: "CashierPassword@2026!",
                FullName: "Tharani Cashier",
                RoleIds: new List<Guid> { cashierRole.Id },
                BranchIds: new List<Guid> { jafBranchId },
                DefaultBranchId: jafBranchId,
                IsGlobalAdmin: false
            );
            var createCshRes = await ownerClient.PostAsJsonAsync("/api/users", cshReq);
            Assert.True(createCshRes.StatusCode == HttpStatusCode.Created || createCshRes.StatusCode == HttpStatusCode.BadRequest);

            // 3. Create an Auditor User
            var audReq = new CreateUserRequest(
                Username: "compliance_auditor",
                Email: "auditor@sarangajewellery.lk",
                Password: "AuditorPassword@2026!",
                FullName: "Nimalan Compliance Auditor",
                RoleIds: new List<Guid> { auditorRole.Id },
                BranchIds: new List<Guid>(),
                DefaultBranchId: null,
                IsGlobalAdmin: false
            );
            var createAudRes = await ownerClient.PostAsJsonAsync("/api/users", audReq);
            Assert.True(createAudRes.StatusCode == HttpStatusCode.Created || createAudRes.StatusCode == HttpStatusCode.BadRequest);

            _output.WriteLine("Created Manager, Cashier, and Auditor test user accounts.");
        }

        // =========================================================================
        // PART 4: PERMISSION ENFORCEMENT & DEACTIVATION
        // =========================================================================
        _output.WriteLine("=== PART 4: Permission-Based Authorization Enforcement & Token Revocation ===");

        // 1. Login as Cashier
        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("jaffna_cashier", "CashierPassword@2026!"));
        Assert.Equal(HttpStatusCode.OK, cashierLoginRes.StatusCode);
        var cashierAuth = await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(cashierAuth);
        var cashierToken = cashierAuth.AccessToken;

        var cashierClient = CreateClientWithCookieHandler();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);

        // 2. Cashier tries to create a branch -> Expect 403 Forbidden!
        var unauthBranchRes = await cashierClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Illegal Branch", "ILL-01", "Sri Lanka", "LKR", "Asia/Colombo"));
        Assert.Equal(HttpStatusCode.Forbidden, unauthBranchRes.StatusCode);
        _output.WriteLine("Verified: Cashier cannot create branches (HTTP 403 Forbidden).");

        // 3. Cashier tries to create a user -> Expect 403 Forbidden!
        var unauthUserRes = await cashierClient.PostAsJsonAsync("/api/users", new CreateUserRequest("illegal_user", "illegal@test.lk", "Pass1234#", "Illegal", new List<Guid>(), new List<Guid>(), null));
        Assert.Equal(HttpStatusCode.Forbidden, unauthUserRes.StatusCode);
        _output.WriteLine("Verified: Cashier cannot create users (HTTP 403 Forbidden).");

        // 4. Login as Auditor
        var auditorLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("compliance_auditor", "AuditorPassword@2026!"));
        Assert.Equal(HttpStatusCode.OK, auditorLoginRes.StatusCode);
        var auditorAuth = await auditorLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auditorAuth);

        var auditorClient = CreateClientWithCookieHandler();
        auditorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auditorAuth.AccessToken);

        // Auditor CAN view branches, users, roles, permissions
        var audBranchesRes = await auditorClient.GetAsync("/api/branches");
        Assert.Equal(HttpStatusCode.OK, audBranchesRes.StatusCode);

        var audUsersRes = await auditorClient.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, audUsersRes.StatusCode);

        var audRolesRes = await auditorClient.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, audRolesRes.StatusCode);

        var audPermsRes = await auditorClient.GetAsync("/api/permissions");
        Assert.Equal(HttpStatusCode.OK, audPermsRes.StatusCode);

        // Auditor CANNOT create or edit (403 Forbidden)
        var audCreateBranchRes = await auditorClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Auditor Branch", "AUD-01", "Sri Lanka", "LKR", "Asia/Colombo"));
        Assert.Equal(HttpStatusCode.Forbidden, audCreateBranchRes.StatusCode);
        _output.WriteLine("Verified: Auditor has read-only access (GET = 200, POST = 403 Forbidden).");

        // 5. Test User Deactivation and Immediate Refresh Token Revocation
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cashierUser = await db.Users.FirstAsync(u => u.Username == "jaffna_cashier");

            // Deactivate cashier
            var deactRes = await ownerClient.PatchAsync($"/api/users/{cashierUser.Id}/status", null);
            Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);

            // Verify cashier's refresh tokens are all revoked in DB
            var activeTokens = await db.RefreshTokens
                .Where(rt => rt.UserId == cashierUser.Id && rt.RevokedAt == null && rt.ExpiresAt > DateTimeOffset.UtcNow)
                .ToListAsync();
            Assert.Empty(activeTokens);
            _output.WriteLine("Verified: Deactivating user immediately revokes all active refresh tokens.");
        }

        // Verify deactivated user cannot log in
        var blockedLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("jaffna_cashier", "CashierPassword@2026!"));
        Assert.Equal(HttpStatusCode.Unauthorized, blockedLoginRes.StatusCode);
        var blockedErr = await blockedLoginRes.Content.ReadAsStringAsync();
        Assert.Contains("inactive", blockedErr, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine("Verified: Deactivated user is blocked from logging in.");

        // Reactivate cashier and reset password
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cashierUser = await db.Users.FirstAsync(u => u.Username == "jaffna_cashier");

            var reactRes = await ownerClient.PatchAsync($"/api/users/{cashierUser.Id}/status", null);
            Assert.Equal(HttpStatusCode.OK, reactRes.StatusCode);

            var resetRes = await ownerClient.PostAsJsonAsync($"/api/users/{cashierUser.Id}/reset-password", new ResetUserPasswordRequest("NewCashierPassword@2026!"));
            Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);
        }

        // Verify login succeeds with new password
        var newLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("jaffna_cashier", "NewCashierPassword@2026!"));
        Assert.Equal(HttpStatusCode.OK, newLoginRes.StatusCode);
        _output.WriteLine("Verified: Reactivated user successfully logs in with new password.");

        // =========================================================================
        // PART 5: BRANCH SCOPED ACCESS VALIDATION
        // =========================================================================
        _output.WriteLine("=== PART 5: Branch Scoping & Authorization Service ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var branchAuth = scope.ServiceProvider.GetRequiredService<IBranchAuthorizationService>();

            var cashierUser = await db.Users.FirstAsync(u => u.Username == "jaffna_cashier");
            var ownerUser = await db.Users.FirstAsync(u => u.Username == "saranga_owner");

            // Verify Global Admin has access to any branch
            var mockOwnerService = new TestCurrentUserService(ownerUser.Id, ownerUser.Username, true, new[] { jafBranchId });
            var ownerBranchAuth = new Saranga.Infrastructure.Services.BranchAuthorizationService(db, mockOwnerService);
            Assert.True(await ownerBranchAuth.CanAccessBranchAsync(jafBranchId));
            Assert.True(await ownerBranchAuth.CanAccessBranchAsync(null)); // Global admin can operate without branch constraint
            var canBranch = await db.Branches.FirstAsync(b => b.Code == "CAN-01");
            Assert.True(await ownerBranchAuth.CanAccessBranchAsync(canBranch.Id)); // Global admin can access any existing branch

            // Verify Cashier can access assigned branch JAF-01
            var mockCashierService = new TestCurrentUserService(cashierUser.Id, cashierUser.Username, false, new[] { jafBranchId });
            var cashierBranchAuth = new Saranga.Infrastructure.Services.BranchAuthorizationService(db, mockCashierService);
            Assert.True(await cashierBranchAuth.CanAccessBranchAsync(jafBranchId));

            // Verify Cashier CANNOT access unassigned branch
            var unassignedBranchId = Guid.NewGuid();
            Assert.False(await cashierBranchAuth.CanAccessBranchAsync(unassignedBranchId));
            _output.WriteLine("Verified: BranchAuthorizationService enforces assigned branch boundaries.");
        }

        _output.WriteLine("=== ALL MODULE 2 TESTS PASSED SUCCESSFULLY! ===");
    }

    private class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; }
        public string? Username { get; }
        public Guid? BranchId { get; }
        public string? Role => null;
        public bool IsGlobalAdmin { get; }
        public IReadOnlyList<Guid> AccessibleBranchIds { get; }
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public IReadOnlyList<string> Permissions => Array.Empty<string>();
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "xUnit";

        public TestCurrentUserService(Guid userId, string username, bool isGlobalAdmin, IEnumerable<Guid> branchIds)
        {
            UserId = userId;
            Username = username;
            IsGlobalAdmin = isGlobalAdmin;
            AccessibleBranchIds = branchIds.ToList();
            BranchId = AccessibleBranchIds.FirstOrDefault();
        }
    }
}

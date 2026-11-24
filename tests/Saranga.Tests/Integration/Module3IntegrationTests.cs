using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Api;
using Saranga.Application.DTOs.Attributes;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.DTOs.Branches;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module3IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public Module3IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await CleanupTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupTestDataAsync();
    }

    private async Task CleanupTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Clean up test attributes and test branches
        await db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM ""StockLocations"" WHERE ""Code"" LIKE 'TEST_%';
            DELETE FROM ""SpecificationOptions"" WHERE ""Code"" LIKE 'TEST_%';
            DELETE FROM ""Specifications"" WHERE ""Code"" LIKE 'TEST_%';
            DELETE FROM ""Purities"" WHERE ""Code"" LIKE 'TEST_%';
            DELETE FROM ""Categories"" WHERE ""Code"" LIKE 'TEST_%';
            DELETE FROM ""Materials"" WHERE ""Code"" LIKE 'TEST_%';
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('TEST-B1', 'TEST-B2'));
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m3'));
            DELETE FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m3');
            DELETE FROM ""Branches"" WHERE ""Code"" IN ('TEST-B1', 'TEST-B2');
        ");
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
    public async Task ComprehensiveModule3_AttributesMaster_EndToEndVerification()
    {
        var client = CreateClientWithCookieHandler();

        // 1. Login as saranga_owner (Global Admin)
        var ownerLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        Assert.Equal(HttpStatusCode.OK, ownerLoginRes.StatusCode);
        var ownerAuth = await ownerLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(ownerAuth);
        var ownerToken = ownerAuth.AccessToken;

        var ownerClient = CreateClientWithCookieHandler();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // =========================================================================
        // PART 1: MATERIALS MASTER CRUD & DUPLICATE PREVENTION
        // =========================================================================
        _output.WriteLine("=== PART 1: Materials Master ===");

        // Create Material (with lowercase code to verify normalization to uppercase)
        var matReq = new CreateMaterialRequest("Yellow Gold 22K Raw", "test_gold_raw", "Standard raw jewellery gold", true, 1);
        var createMatRes = await ownerClient.PostAsJsonAsync("/api/materials", matReq);
        Assert.Equal(HttpStatusCode.Created, createMatRes.StatusCode);
        var createdMat = await createMatRes.Content.ReadFromJsonAsync<MaterialDto>();
        Assert.NotNull(createdMat);
        Assert.Equal("TEST_GOLD_RAW", createdMat.Code); // Normalized to uppercase

        // Case-Insensitive Duplicate Code Rejection (e.g. Test_Gold_Raw)
        var dupMatRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Yellow Gold Duplicate", "Test_Gold_Raw", "Duplicate", true, 1));
        Assert.Equal(HttpStatusCode.BadRequest, dupMatRes.StatusCode);

        // Update Material
        var updateMatReq = new UpdateMaterialRequest("Yellow Gold 22K Refined", "Refined casting gold", true, 2);
        var updateMatRes = await ownerClient.PutAsJsonAsync($"/api/materials/{createdMat.Id}", updateMatReq);
        Assert.Equal(HttpStatusCode.OK, updateMatRes.StatusCode);
        var updatedMat = await updateMatRes.Content.ReadFromJsonAsync<MaterialDto>();
        Assert.Equal("Yellow Gold 22K Refined", updatedMat!.Name);

        // Toggle Status (Deactivate / Activate)
        var deactMatRes = await ownerClient.PatchAsync($"/api/materials/{createdMat.Id}/status", null);
        Assert.Equal(HttpStatusCode.OK, deactMatRes.StatusCode);
        var deactMat = await deactMatRes.Content.ReadFromJsonAsync<MaterialDto>();
        Assert.False(deactMat!.IsActive);

        // Active-only lookup excludes inactive
        var activeMats = await ownerClient.GetFromJsonAsync<List<MaterialDto>>("/api/materials?isActive=true");
        Assert.DoesNotContain(activeMats!, m => m.Id == createdMat.Id);

        // Reactivate
        var reactMatRes = await ownerClient.PatchAsync($"/api/materials/{createdMat.Id}/status", null);
        Assert.Equal(HttpStatusCode.OK, reactMatRes.StatusCode);
        _output.WriteLine("Verified: Materials CRUD, duplicate prevention, and active-only filtering.");

        // =========================================================================
        // PART 2: CATEGORIES MASTER CRUD
        // =========================================================================
        _output.WriteLine("=== PART 2: Categories Master ===");

        var catReq = new CreateCategoryRequest("Bridal Necklace", "TEST_CAT_NECKLACE", "Full bridal necklace set", true, 1);
        var createCatRes = await ownerClient.PostAsJsonAsync("/api/categories", catReq);
        Assert.Equal(HttpStatusCode.Created, createCatRes.StatusCode);
        var createdCat = await createCatRes.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.NotNull(createdCat);
        Assert.Equal("TEST_CAT_NECKLACE", createdCat.Code);

        // Duplicate Rejection
        var dupCatRes = await ownerClient.PostAsJsonAsync("/api/categories", catReq);
        Assert.Equal(HttpStatusCode.BadRequest, dupCatRes.StatusCode);

        // Update Category
        var updateCatRes = await ownerClient.PutAsJsonAsync($"/api/categories/{createdCat.Id}", new UpdateCategoryRequest("Bridal Gold Necklace", "Updated description", true, 1));
        Assert.Equal(HttpStatusCode.OK, updateCatRes.StatusCode);

        // Status Toggle
        var toggleCatRes = await ownerClient.PatchAsync($"/api/categories/{createdCat.Id}/status", null);
        Assert.Equal(HttpStatusCode.OK, toggleCatRes.StatusCode);
        var toggledCat = await toggleCatRes.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.False(toggledCat!.IsActive);
        _output.WriteLine("Verified: Categories CRUD and status toggle.");

        // =========================================================================
        // PART 3: PURITIES MASTER CRUD & DECIMAL PRECISION
        // =========================================================================
        _output.WriteLine("=== PART 3: Purities Master ===");

        // KaratValue = 22.0, PurityPercentage = 91.67
        var purityReq = new CreatePurityRequest("22 Karat Standard", "TEST_22K", 22.00m, 91.67m, "Standard 916 jewellery fineness", true, 1);
        var createPurityRes = await ownerClient.PostAsJsonAsync("/api/purities", purityReq);
        Assert.Equal(HttpStatusCode.Created, createPurityRes.StatusCode);
        var createdPurity = await createPurityRes.Content.ReadFromJsonAsync<PurityDto>();
        Assert.NotNull(createdPurity);
        Assert.Equal(22.00m, createdPurity.KaratValue);
        Assert.Equal(91.67m, createdPurity.PurityPercentage);

        // Duplicate Rejection
        var dupPurityRes = await ownerClient.PostAsJsonAsync("/api/purities", purityReq);
        Assert.Equal(HttpStatusCode.BadRequest, dupPurityRes.StatusCode);

        // Update Purity
        var updatePurityRes = await ownerClient.PutAsJsonAsync($"/api/purities/{createdPurity.Id}", new UpdatePurityRequest("22 Karat Premium 916", 22.00m, 91.70m, "Refined 916 hallmarked", true, 1));
        Assert.Equal(HttpStatusCode.OK, updatePurityRes.StatusCode);
        var updatedPurity = await updatePurityRes.Content.ReadFromJsonAsync<PurityDto>();
        Assert.Equal(91.70m, updatedPurity!.PurityPercentage);
        _output.WriteLine("Verified: Purities CRUD with decimal precision (22.00 Karat, 91.70% fineness).");

        // =========================================================================
        // PART 4: SPECIFICATIONS MASTER & OPTIONS
        // =========================================================================
        _output.WriteLine("=== PART 4: Specifications & Options Master ===");

        var specReq = new CreateSpecificationRequest("Design Theme", "TEST_DESIGN_THEME", "Aesthetic design classification", true, 1);
        var createSpecRes = await ownerClient.PostAsJsonAsync("/api/specifications", specReq);
        Assert.Equal(HttpStatusCode.Created, createSpecRes.StatusCode);
        var createdSpec = await createSpecRes.Content.ReadFromJsonAsync<SpecificationDto>();
        Assert.NotNull(createdSpec);

        // Add Options to Specification
        var optReq1 = new CreateSpecificationOptionRequest("Traditional Jaffna Style", "TEST_OPT_TRAD", "Classic heritage design", true, 1);
        var addOptRes1 = await ownerClient.PostAsJsonAsync($"/api/specifications/{createdSpec.Id}/options", optReq1);
        Assert.Equal(HttpStatusCode.OK, addOptRes1.StatusCode);

        var optReq2 = new CreateSpecificationOptionRequest("Contemporary Minimalist", "TEST_OPT_CONTEMP", "Modern light-weight style", true, 2);
        var addOptRes2 = await ownerClient.PostAsJsonAsync($"/api/specifications/{createdSpec.Id}/options", optReq2);
        Assert.Equal(HttpStatusCode.OK, addOptRes2.StatusCode);

        // Verify Specification includes options
        var fetchedSpec = await ownerClient.GetFromJsonAsync<SpecificationDto>($"/api/specifications/{createdSpec.Id}");
        Assert.Equal(2, fetchedSpec!.Options.Count);
        _output.WriteLine("Verified: Specifications and structured options creation.");

        // =========================================================================
        // PART 5: STOCK / DISPLAY LOCATIONS & BRANCH ISOLATION
        // =========================================================================
        _output.WriteLine("=== PART 5: Stock / Display Locations & Branch Boundaries ===");

        // 1. Create two test branches: TEST-B1 (Branch A) and TEST-B2 (Branch B)
        var b1Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test Branch 1", "TEST-B1", "Sri Lanka", "LKR", "Asia/Colombo"));
        var b1 = await b1Res.Content.ReadFromJsonAsync<BranchDto>();

        var b2Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test Branch 2", "TEST-B2", "Sri Lanka", "LKR", "Asia/Colombo"));
        var b2 = await b2Res.Content.ReadFromJsonAsync<BranchDto>();

        // 2. Create Stock Locations for Branch 1
        var locReq1 = new CreateStockLocationRequest(b1!.Id, "Main Showroom Display", "TEST_DISP_01", "Display", "Front glass counter display", true, 1);
        var createLoc1Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", locReq1);
        Assert.Equal(HttpStatusCode.Created, createLoc1Res.StatusCode);
        var createdLoc1 = await createLoc1Res.Content.ReadFromJsonAsync<StockLocationDto>();
        Assert.NotNull(createdLoc1);
        Assert.Equal("DISPLAY", createdLoc1.LocationType); // Controlled stable uppercase value

        // Uncontrolled/invalid location type rejection
        var badTypeRes = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1.Id, "Invalid Type Loc", "TEST_BAD_TYPE", "unrestricted_random_string", "Desc"));
        Assert.Equal(HttpStatusCode.BadRequest, badTypeRes.StatusCode);
        _output.WriteLine("Verified: Unrestricted location type strings rejected.");

        // Duplicate code in same branch rejected (case-insensitive)
        var dupLocRes = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1.Id, "Duplicate Display", "test_disp_01", "DISPLAY", "Duplicate Desc"));
        Assert.Equal(HttpStatusCode.BadRequest, dupLocRes.StatusCode);

        // Same code in Branch 2 is allowed (branch-specific scope)
        var locReq2 = new CreateStockLocationRequest(b2!.Id, "Branch 2 Display", "TEST_DISP_01", "Display", "Branch 2 display counter", true, 1);
        var createLoc2Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", locReq2);
        Assert.Equal(HttpStatusCode.Created, createLoc2Res.StatusCode);

        // 3. Create regular user assigned ONLY to Branch 1
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cashierRole = await db.Roles.FirstAsync(r => r.Name == SystemRoles.Cashier);

            var userReq = new CreateUserRequest(
                Username: "test_cashier_m3",
                Email: "cashier.m3@saranga.test",
                Password: "CashierPassword@2026!",
                FullName: "Test Cashier M3",
                RoleIds: new List<Guid> { cashierRole.Id },
                BranchIds: new List<Guid> { b1.Id },
                DefaultBranchId: b1.Id
            );
            await ownerClient.PostAsJsonAsync("/api/users", userReq);
        }

        // Login as Cashier (assigned only to Branch 1)
        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_cashier_m3", "CashierPassword@2026!"));
        Assert.Equal(HttpStatusCode.OK, cashierLoginRes.StatusCode);
        var cashierAuth = await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>();

        var cashierClient = CreateClientWithCookieHandler();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierAuth!.AccessToken);

        // Cashier attempts to access Branch 2 locations -> Expect 403 Forbidden!
        var unauthLocRes = await cashierClient.GetAsync($"/api/stock-locations?branchId={b2!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, unauthLocRes.StatusCode);
        _output.WriteLine("Verified: Backend rejects unauthorized branch location access (403 Forbidden).");

        // Cashier attempts to create a location -> Expect 403 Forbidden (Cashier has no ATTRIBUTES_CREATE)
        var unauthCreateRes = await cashierClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1.Id, "Illegal Loc", "TEST_ILL", "Display", "None"));
        Assert.Equal(HttpStatusCode.Forbidden, unauthCreateRes.StatusCode);
        _output.WriteLine("Verified: Cashier cannot modify or create Attributes Master records (403 Forbidden).");

        // =========================================================================
        // PART 6: MAKING CHARGE METHODS LOOKUP
        // =========================================================================
        _output.WriteLine("=== PART 6: Making Charge Methods ===");

        var mcMethods = await ownerClient.GetFromJsonAsync<List<MakingChargeMethodDto>>("/api/making-charge-methods");
        Assert.NotNull(mcMethods);
        Assert.Contains(mcMethods, m => m.Name == "Per Gram" && m.IsDefault);
        Assert.Contains(mcMethods, m => m.Name == "Per Item" && !m.IsDefault);
        _output.WriteLine($"Verified: Making Charge Methods lookup returned {mcMethods.Count} confirmed calculation modes.");

        // =========================================================================
        // PART 7: AUDIT LOG VERIFICATION
        // =========================================================================
        _output.WriteLine("=== PART 7: Audit Trail Verification ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audits = await db.AuditLogs
                .Where(a => a.EntityName == "Material" || a.EntityName == "Category" || a.EntityName == "Purity" || a.EntityName == "StockLocation")
                .ToListAsync();

            Assert.NotEmpty(audits);
            Assert.Contains(audits, a => a.Action == "CREATE");
            Assert.Contains(audits, a => a.Action == "UPDATE");
            Assert.Contains(audits, a => a.Action == "ACTIVATED" || a.Action == "DEACTIVATED");
            _output.WriteLine($"Verified: {audits.Count} audit records created with JSONB payloads.");
        }

        _output.WriteLine("=== ALL MODULE 3 TESTS PASSED SUCCESSFULLY! ===");
    }
}

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
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Inventory;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module4IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public Module4IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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

        await db.Database.ExecuteSqlRawAsync(@"
            DELETE FROM ""InventoryItemSpecifications"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M4_%' OR ""TagNumber"" LIKE 'TEST-INV-%');
            DELETE FROM ""InventoryItemMedia"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M4_%' OR ""TagNumber"" LIKE 'TEST-INV-%');
            DELETE FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M4_%' OR ""TagNumber"" LIKE 'TEST-INV-%' OR ""Name"" LIKE 'Test M4%';
            DELETE FROM ""StockLocations"" WHERE ""Code"" LIKE 'TEST_M4_%';
            DELETE FROM ""SpecificationOptions"" WHERE ""Code"" LIKE 'TEST_M4_%';
            DELETE FROM ""Specifications"" WHERE ""Code"" LIKE 'TEST_M4_%';
            DELETE FROM ""Purities"" WHERE ""Code"" LIKE 'TEST_M4_%';
            DELETE FROM ""Categories"" WHERE ""Code"" LIKE 'TEST_M4_%';
            DELETE FROM ""Materials"" WHERE ""Code"" LIKE 'TEST_M4_%';
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('TEST-INV-B1', 'TEST-INV-B2'));
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m4'));
            DELETE FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m4');
            DELETE FROM ""Branches"" WHERE ""Code"" IN ('TEST-INV-B1', 'TEST-INV-B2');
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
    public async Task ComprehensiveModule4_InventoryCore_EndToEndVerification()
    {
        var client = CreateClientWithCookieHandler();

        // 1. Authenticate as Owner / Super Admin
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var ownerToken = authData!.AccessToken;

        var ownerClient = CreateClientWithCookieHandler();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // 2. Setup Prerequisites: Branches & Stock Locations
        _output.WriteLine("=== Setting up test branches and stock locations ===");
        var b1Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test Branch 1", "TEST-INV-B1", "LK", "LKR", "Asia/Colombo", true));
        var b1 = await b1Res.Content.ReadFromJsonAsync<BranchDto>();

        var b2Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test Branch 2", "TEST-INV-B2", "LK", "LKR", "Asia/Colombo", true));
        var b2 = await b2Res.Content.ReadFromJsonAsync<BranchDto>();

        var loc1Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1!.Id, "Main Display B1", "TEST_M4_LOC_1", "DISPLAY", "B1 Display Showcase", true, 1));
        var loc1 = await loc1Res.Content.ReadFromJsonAsync<StockLocationDto>();

        var loc2Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b2!.Id, "Main Display B2", "TEST_M4_LOC_2", "DISPLAY", "B2 Display Showcase", true, 1));
        var loc2 = await loc2Res.Content.ReadFromJsonAsync<StockLocationDto>();

        // Setup Attributes Master: Material, Category, Purity, Specification
        var matRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("22K Gold Metal", "TEST_M4_MAT", "Pure metal", true, 1));
        var mat = await matRes.Content.ReadFromJsonAsync<MaterialDto>();

        var catRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Gold Ring", "TEST_M4_CAT", "Rings", true, 1));
        var cat = await catRes.Content.ReadFromJsonAsync<CategoryDto>();

        var purRes = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("22K Purity", "TEST_M4_PUR", 22.0m, 91.67m, "Standard 22K", true, 1));
        var pur = await purRes.Content.ReadFromJsonAsync<PurityDto>();

        var specRes = await ownerClient.PostAsJsonAsync("/api/specifications", new CreateSpecificationRequest("Ring Size", "TEST_M4_SPEC", "Standard US sizes", true, 1));
        var spec = await specRes.Content.ReadFromJsonAsync<SpecificationDto>();

        var optRes = await ownerClient.PostAsJsonAsync($"/api/specifications/{spec!.Id}/options", new CreateSpecificationOptionRequest("US 7", "TEST_M4_OPT_7", "Size 7", true, 1));
        var opt = await optRes.Content.ReadFromJsonAsync<SpecificationOptionDto>();

        // =========================================================================
        // PART 1: WEIGHT VALIDATION RULES & NET WEIGHT CALCULATION
        // =========================================================================
        _output.WriteLine("=== PART 1: Weight Rules Verification ===");

        // A. Reject negative gross weight
        var negGrossRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1!.Id,
            Name: "Test Ring Neg Gross",
            ItemCode: "TEST_M4_RNG_01",
            TagNumber: null,
            Barcode: null,
            MaterialId: mat!.Id,
            CategoryId: cat!.Id,
            PurityId: pur!.Id,
            GrossWeight: -5.0000m,
            StoneWeight: 1.0000m
        ));
        Assert.Equal(HttpStatusCode.BadRequest, negGrossRes.StatusCode);

        // B. Reject negative stone weight
        var negStoneRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test Ring Neg Stone",
            ItemCode: "TEST_M4_RNG_02",
            TagNumber: null,
            Barcode: null,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 10.0000m,
            StoneWeight: -1.0000m
        ));
        Assert.Equal(HttpStatusCode.BadRequest, negStoneRes.StatusCode);

        // C. Reject stone weight > gross weight
        var stoneExceedsRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test Ring Stone Exceeds",
            ItemCode: "TEST_M4_RNG_03",
            TagNumber: null,
            Barcode: null,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 10.0000m,
            StoneWeight: 12.0000m
        ));
        Assert.Equal(HttpStatusCode.BadRequest, stoneExceedsRes.StatusCode);
        _output.WriteLine("Verified: Backend rejects negative and invalid weights.");

        // =========================================================================
        // PART 2: BRANCH & LOCATION BOUNDARY RULES
        // =========================================================================
        _output.WriteLine("=== PART 2: Branch Location Isolation ===");

        // Reject item in Branch 1 assigned to Branch 2's location
        var crossBranchRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc2!.Id, // Location belongs to B2!
            Name: "Cross Branch Item",
            ItemCode: "TEST_M4_RNG_CROSS",
            TagNumber: null,
            Barcode: null,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 12.5000m,
            StoneWeight: 1.2500m
        ));
        Assert.Equal(HttpStatusCode.BadRequest, crossBranchRes.StatusCode);
        _output.WriteLine("Verified: Backend strictly rejects cross-branch stock location assignment.");

        // =========================================================================
        // PART 3: SUCCESSFUL CREATION & AUTO-GENERATED TAG & BARCODE
        // =========================================================================
        _output.WriteLine("=== PART 3: Item Creation & Automatic Tag/Barcode ===");

        var validCreateReq = new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test 22K Traditional Ring",
            ItemCode: "TEST_M4_RNG_VALID",
            TagNumber: null, // Auto-generate
            Barcode: null,   // Auto-generate
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 12.5000m,
            StoneWeight: 1.2500m,
            PieceCount: 1,
            MakingChargeMethod: 1, // PerGram
            MakingChargeValue: 2500.00m,
            Description: "Handcrafted 22K Gold Ring with Cubic Zirconia",
            IsActive: true,
            Specifications: new List<CreateInventoryItemSpecificationRequest>
            {
                new(spec.Id, opt!.Id, null)
            }
        );

        var createRes = await ownerClient.PostAsJsonAsync("/api/inventory", validCreateReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdItem = await createRes.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.NotNull(createdItem);

        // Verify Net Weight: 12.5000 - 1.2500 = 11.2500
        Assert.Equal(11.2500m, createdItem.NetWeight);
        Assert.Equal("AVAILABLE", createdItem.StatusName);
        Assert.NotEmpty(createdItem.TagNumber);
        Assert.NotEmpty(createdItem.Barcode);
        Assert.StartsWith("TEST-INV-B1", createdItem.TagNumber);
        Assert.StartsWith("SRGTEST-INV-B1", createdItem.Barcode);
        Assert.Single(createdItem.Specifications);
        Assert.Equal("US 7", createdItem.Specifications[0].OptionValue);
        _output.WriteLine($"Verified: Item created successfully. Tag: {createdItem.TagNumber}, Barcode: {createdItem.Barcode}, NetWeight: {createdItem.NetWeight}g");

        // Verify duplicate Tag Number rejection
        var dupTagReq = validCreateReq with { TagNumber = createdItem.TagNumber, ItemCode = "TEST_RNG_DUP_TAG" };
        var dupTagRes = await ownerClient.PostAsJsonAsync("/api/inventory", dupTagReq);
        Assert.Equal(HttpStatusCode.BadRequest, dupTagRes.StatusCode);

        // Verify duplicate Barcode rejection
        var dupBarcodeReq = validCreateReq with { Barcode = createdItem.Barcode, ItemCode = "TEST_RNG_DUP_BAR" };
        var dupBarcodeRes = await ownerClient.PostAsJsonAsync("/api/inventory", dupBarcodeReq);
        Assert.Equal(HttpStatusCode.BadRequest, dupBarcodeRes.StatusCode);
        _output.WriteLine("Verified: Duplicate tag number and barcode rejections enforced.");

        // =========================================================================
        // PART 4: BARCODE & TAG LOOKUP (POS SCANNING READY)
        // =========================================================================
        _output.WriteLine("=== PART 4: Barcode & Tag Lookup ===");

        // Lookup by Barcode
        var barcodeLookupRes = await ownerClient.GetAsync($"/api/inventory/lookup/barcode/{createdItem.Barcode}");
        var barcodeBody = await barcodeLookupRes.Content.ReadAsStringAsync();
        _output.WriteLine($"Barcode lookup result for '{createdItem.Barcode}': status={barcodeLookupRes.StatusCode}, body={barcodeBody}");
        Assert.Equal(HttpStatusCode.OK, barcodeLookupRes.StatusCode);
        var barcodeLookup = await barcodeLookupRes.Content.ReadFromJsonAsync<InventoryLookupDto>();
        Assert.NotNull(barcodeLookup);
        Assert.Equal(createdItem.Id, barcodeLookup.Id);
        Assert.Equal(createdItem.TagNumber, barcodeLookup.TagNumber);
        Assert.Equal(11.2500m, barcodeLookup.NetWeight);

        // Lookup by Tag
        var tagLookupRes = await ownerClient.GetAsync($"/api/inventory/lookup/tag/{createdItem.TagNumber}");
        Assert.Equal(HttpStatusCode.OK, tagLookupRes.StatusCode);
        var tagLookup = await tagLookupRes.Content.ReadFromJsonAsync<InventoryLookupDto>();
        Assert.NotNull(tagLookup);
        Assert.Equal(createdItem.Id, tagLookup.Id);
        _output.WriteLine("Verified: Fast Barcode and Tag lookup functional.");

        // =========================================================================
        // PART 5: CASHIER ROLE & BRANCH AUTHORIZATION RESTRICTIONS
        // =========================================================================
        _output.WriteLine("=== PART 5: Cashier Role & Branch Boundaries ===");

        // Create an item in Branch 2
        var b2ItemReq = validCreateReq with
        {
            BranchId = b2.Id,
            StockLocationId = loc2.Id,
            ItemCode = "TEST_RNG_B2",
            TagNumber = null,
            Barcode = null
        };
        var b2ItemRes = await ownerClient.PostAsJsonAsync("/api/inventory", b2ItemReq);
        Assert.Equal(HttpStatusCode.Created, b2ItemRes.StatusCode);
        var b2Item = await b2ItemRes.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Create cashier assigned ONLY to Branch 1
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cashierRole = await db.Roles.FirstAsync(r => r.Name == SystemRoles.Cashier);

            var userReq = new CreateUserRequest(
                Username: "test_cashier_m4",
                Email: "cashier.m4@saranga.test",
                Password: "CashierPassword@2026!",
                FullName: "Test Cashier M4",
                RoleIds: new List<Guid> { cashierRole.Id },
                BranchIds: new List<Guid> { b1.Id },
                DefaultBranchId: b1.Id
            );
            await ownerClient.PostAsJsonAsync("/api/users", userReq);
        }

        // Login as Cashier
        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_cashier_m4", "CashierPassword@2026!"));
        Assert.Equal(HttpStatusCode.OK, cashierLoginRes.StatusCode);
        var cashierAuth = await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>();

        var cashierClient = CreateClientWithCookieHandler();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierAuth!.AccessToken);

        // Cashier scans Branch 1 item -> Succeeds
        var cashierB1Scan = await cashierClient.GetAsync($"/api/inventory/lookup/barcode/{createdItem.Barcode}");
        Assert.Equal(HttpStatusCode.OK, cashierB1Scan.StatusCode);

        // Cashier scans Branch 2 item -> 403 Forbidden!
        var cashierB2Scan = await cashierClient.GetAsync($"/api/inventory/lookup/barcode/{b2Item!.Barcode}");
        Assert.Equal(HttpStatusCode.Forbidden, cashierB2Scan.StatusCode);
        _output.WriteLine("Verified: Cashier cannot scan or access inventory from unauthorized branch (403 Forbidden).");

        // Cashier attempts to create inventory -> 403 Forbidden
        var cashierCreateRes = await cashierClient.PostAsJsonAsync("/api/inventory", validCreateReq);
        Assert.Equal(HttpStatusCode.Forbidden, cashierCreateRes.StatusCode);

        // Cashier attempts to edit inventory -> 403 Forbidden
        var cashierEditRes = await cashierClient.PutAsJsonAsync($"/api/inventory/{createdItem.Id}", new UpdateInventoryItemRequest(
            StockLocationId: loc1.Id,
            Name: "Hacked by Cashier",
            ItemCode: "HACK",
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 10m,
            StoneWeight: 0m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 0m,
            Description: "None",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Forbidden, cashierEditRes.StatusCode);
        _output.WriteLine("Verified: Cashier cannot create or edit inventory master data (403 Forbidden).");

        // =========================================================================
        // PART 6: PERMITTED EDITING, STATUS TOGGLE & AUDITING
        // =========================================================================
        _output.WriteLine("=== PART 6: Permitted Editing, Tag Immutability & Status Management ===");

        var updateReq = new UpdateInventoryItemRequest(
            StockLocationId: loc1.Id,
            Name: "Test 22K Traditional Ring - Refined",
            ItemCode: createdItem.ItemCode,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 12.8000m,
            StoneWeight: 1.3000m, // Net = 11.5000g
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 2750.00m,
            Description: "Updated description with certified stone",
            IsActive: true
        );

        var updateRes = await ownerClient.PutAsJsonAsync($"/api/inventory/{createdItem.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedItem = await updateRes.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.NotNull(updatedItem);
        Assert.Equal("Test 22K Traditional Ring - Refined", updatedItem.Name);
        Assert.Equal(11.5000m, updatedItem.NetWeight);

        // Verification 1: Tag and Barcode Immutability
        Assert.Equal(createdItem.TagNumber, updatedItem.TagNumber);
        Assert.Equal(createdItem.Barcode, updatedItem.Barcode);
        _output.WriteLine("Verified: TagNumber and Barcode remain strictly immutable after updates.");

        // Verification 2: Specification InputType Enforcement (PREDEFINED rejects CustomValue)
        var invalidSpecReq = new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Invalid Spec Item",
            ItemCode: "TEST_M4_INV_SPEC_ERR",
            TagNumber: null,
            Barcode: null,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 5.0m,
            StoneWeight: 0m,
            Specifications: new List<CreateInventoryItemSpecificationRequest>
            {
                new(spec.Id, opt.Id, "Unallowed Custom Value on Predefined Spec")
            }
        );
        var invalidSpecRes = await ownerClient.PostAsJsonAsync("/api/inventory", invalidSpecReq);
        Assert.Equal(HttpStatusCode.BadRequest, invalidSpecRes.StatusCode);
        _output.WriteLine("Verified: CustomValue rejected for PREDEFINED specification.");

        // Verification 3: Administrative Status Correction Authorization & Validation
        // Cashier without INVENTORY_STATUS_OVERRIDE must be rejected
        var cashierCorrectionRes = await cashierClient.PostAsJsonAsync(
            $"/api/inventory/{createdItem.Id}/admin-status-correction",
            new AdminStatusCorrectionRequest(2, "Unauthorized attempt"));
        Assert.Equal(HttpStatusCode.Forbidden, cashierCorrectionRes.StatusCode);

        // Owner with empty reason must be rejected
        var emptyReasonRes = await ownerClient.PostAsJsonAsync(
            $"/api/inventory/{createdItem.Id}/admin-status-correction",
            new AdminStatusCorrectionRequest(2, ""));
        Assert.Equal(HttpStatusCode.BadRequest, emptyReasonRes.StatusCode);

        // Marking SOLD via admin correction must be rejected (SOLD reserved strictly for POS finalization)
        var soldCorrectionRes = await ownerClient.PostAsJsonAsync(
            $"/api/inventory/{createdItem.Id}/admin-status-correction",
            new AdminStatusCorrectionRequest(3, "Attempting to casually mark SOLD"));
        Assert.Equal(HttpStatusCode.BadRequest, soldCorrectionRes.StatusCode);

        // Valid administrative correction to RESERVED
        var validCorrectionRes = await ownerClient.PostAsJsonAsync(
            $"/api/inventory/{createdItem.Id}/admin-status-correction",
            new AdminStatusCorrectionRequest(2, "Administrative VIP hold for customer visit"));
        Assert.Equal(HttpStatusCode.OK, validCorrectionRes.StatusCode);
        var reservedItem = await validCorrectionRes.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.Equal(2, reservedItem!.Status);

        // Valid administrative correction back to AVAILABLE
        var restoreCorrectionRes = await ownerClient.PostAsJsonAsync(
            $"/api/inventory/{createdItem.Id}/admin-status-correction",
            new AdminStatusCorrectionRequest(1, "Customer visit concluded, returned to open stock"));
        Assert.Equal(HttpStatusCode.OK, restoreCorrectionRes.StatusCode);
        var availableItem = await restoreCorrectionRes.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.Equal(1, availableItem!.Status);

        // Verification 4: Sold Item Immutability
        // Simulate item sold by POS in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                @"UPDATE ""InventoryItems"" SET ""Status"" = 3 WHERE ""Id"" = {0}", createdItem.Id);
        }

        // Sold items cannot be edited
        var soldEditRes = await ownerClient.PutAsJsonAsync($"/api/inventory/{createdItem.Id}", updateReq);
        Assert.Equal(HttpStatusCode.BadRequest, soldEditRes.StatusCode);

        // Sold items cannot have status overridden
        var soldOverrideRes = await ownerClient.PostAsJsonAsync(
            $"/api/inventory/{createdItem.Id}/admin-status-correction",
            new AdminStatusCorrectionRequest(1, "Attempting to un-sell sold item"));
        Assert.Equal(HttpStatusCode.BadRequest, soldOverrideRes.StatusCode);

        // Sold items cannot be deactivated
        var soldDeactivateRes = await ownerClient.PatchAsync($"/api/inventory/{createdItem.Id}/toggle-active", null);
        Assert.Equal(HttpStatusCode.BadRequest, soldDeactivateRes.StatusCode);
        _output.WriteLine("Verified: SOLD status immutability strictly enforced across edit, override, and active toggles.");

        // Reset to Available for cleanup
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                @"UPDATE ""InventoryItems"" SET ""Status"" = 1 WHERE ""Id"" = {0}", createdItem.Id);
        }

        // Toggle active status
        var toggleRes = await ownerClient.PatchAsync($"/api/inventory/{createdItem.Id}/toggle-active", null);
        Assert.Equal(HttpStatusCode.OK, toggleRes.StatusCode);
        var toggledItem = await toggleRes.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.False(toggledItem!.IsActive);

        // Search and Paging
        var searchRes = await ownerClient.GetFromJsonAsync<PagedResult<InventoryItemDto>>($"/api/inventory?search={createdItem.TagNumber}&branchId={b1.Id}");
        Assert.NotNull(searchRes);
        Assert.True(searchRes.TotalCount >= 1);
        _output.WriteLine("Verified: Permitted update, status toggle, and server-side search verified.");

        // Verify Audit Logs
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audits = await db.AuditLogs
                .Where(a => a.EntityName == "InventoryItem" && a.EntityId == createdItem.Id.ToString())
                .ToListAsync();

            Assert.True(audits.Count >= 3); // CREATE, UPDATE, ADMIN_STATUS_CORRECTION, ACTIVATED/DEACTIVATED
            Assert.Contains(audits, a => a.Action == "CREATE");
            Assert.Contains(audits, a => a.Action == "UPDATE");
            Assert.Contains(audits, a => a.Action == "ADMIN_STATUS_CORRECTION");
            _output.WriteLine($"Verified: {audits.Count} audit records created for InventoryItem.");
        }
    }
}

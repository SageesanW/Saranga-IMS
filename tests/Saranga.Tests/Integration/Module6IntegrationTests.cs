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
using Saranga.Application.DTOs.Compositions;
using Saranga.Application.DTOs.Inventory;
using Saranga.Application.DTOs.Roles;
using Saranga.Application.DTOs.Users;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module6IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public Module6IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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
            DELETE FROM ""InventoryCompositionMembers"" WHERE ""CompositionId"" IN (
                SELECT ""Id"" FROM ""InventoryCompositions"" WHERE ""Name"" LIKE 'Test M6%' OR ""TagNumber"" LIKE 'TEST-M6-%' OR ""TagNumber"" LIKE 'T6B1-%'
            );
            DELETE FROM ""InventoryCompositions"" WHERE ""Name"" LIKE 'Test M6%' OR ""TagNumber"" LIKE 'TEST-M6-%' OR ""TagNumber"" LIKE 'T6B1-%';
            DELETE FROM ""InventoryItemSpecifications"" WHERE ""InventoryItemId"" IN (
                SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M6_%' OR ""TagNumber"" LIKE 'TEST-M6-%' OR ""TagNumber"" LIKE 'T6B1-%'
            );
            DELETE FROM ""InventoryItemMedia"" WHERE ""InventoryItemId"" IN (
                SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M6_%' OR ""TagNumber"" LIKE 'TEST-M6-%' OR ""TagNumber"" LIKE 'T6B1-%'
            );
            DELETE FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M6_%' OR ""TagNumber"" LIKE 'TEST-M6-%' OR ""TagNumber"" LIKE 'T6B1-%' OR ""Name"" LIKE 'Test M6%';
            DELETE FROM ""StockLocations"" WHERE ""Code"" LIKE 'TEST_M6_%';
            DELETE FROM ""Purities"" WHERE ""Code"" LIKE 'TEST_M6_%';
            DELETE FROM ""Categories"" WHERE ""Code"" LIKE 'TEST_M6_%';
            DELETE FROM ""Materials"" WHERE ""Code"" LIKE 'TEST_M6_%';
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T6B1', 'T6B2'));
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m6', 'test_manager_m6'));
            DELETE FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m6', 'test_manager_m6');
            DELETE FROM ""RolePermissions"" WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""Roles"" WHERE ""Name"" LIKE 'Test M6%');
            DELETE FROM ""Roles"" WHERE ""Name"" LIKE 'Test M6%';
            DELETE FROM ""Branches"" WHERE ""Code"" IN ('T6B1', 'T6B2');
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
    public async Task ComprehensiveModule6_Compositions_EndToEndVerification()
    {
        var client = CreateClientWithCookieHandler();

        // 1. Authenticate as Owner / Super Admin
        _output.WriteLine("=== 1. Authenticate as Owner / Super Admin ===");
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var ownerToken = authData!.AccessToken;

        var ownerClient = CreateClientWithCookieHandler();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // 2. Setup Prerequisites: Branches & Locations
        _output.WriteLine("=== 2. Setup Branches & Stock Locations ===");
        var b1Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M6 Branch 1", "T6B1", "LK", "LKR", "Asia/Colombo", true));
        var b1 = await b1Res.Content.ReadFromJsonAsync<BranchDto>();

        var b2Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M6 Branch 2", "T6B2", "LK", "LKR", "Asia/Colombo", true));
        var b2 = await b2Res.Content.ReadFromJsonAsync<BranchDto>();

        var loc1Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1!.Id, "Main Display B1", "TEST_M6_LOC_1", "DISPLAY", "B1 Display Showcase", true, 1));
        var loc1 = await loc1Res.Content.ReadFromJsonAsync<StockLocationDto>();

        var loc1AltRes = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1.Id, "Back Safe B1", "TEST_M6_LOC_1_ALT", "VAULT", "B1 Secure Vault", true, 2));
        Assert.Equal(HttpStatusCode.Created, loc1AltRes.StatusCode);
        var loc1Alt = await loc1AltRes.Content.ReadFromJsonAsync<StockLocationDto>();

        var loc2Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b2!.Id, "Main Display B2", "TEST_M6_LOC_2", "DISPLAY", "B2 Display Showcase", true, 1));
        var loc2 = await loc2Res.Content.ReadFromJsonAsync<StockLocationDto>();

        // 3. Setup Attributes Masters & Hardened Namespace Separation
        _output.WriteLine("=== 3. Setup Attributes & Reserved Code Hardening ===");
        // Hardening Check: Ensure ASM and GRP cannot be used as Category or Material codes
        var reservedCatRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Reserved Assembly Test", "ASM", "Should be rejected", true, 99));
        Assert.Equal(HttpStatusCode.BadRequest, reservedCatRes.StatusCode);
        var reservedCatErr = await reservedCatRes.Content.ReadAsStringAsync();
        Assert.Contains("reserved for inventory compositions", reservedCatErr);

        var reservedMatRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Reserved Group Test", "GRP", "Should be rejected", true, 99));
        Assert.Equal(HttpStatusCode.BadRequest, reservedMatRes.StatusCode);
        var reservedMatErr = await reservedMatRes.Content.ReadAsStringAsync();
        Assert.Contains("reserved for inventory compositions", reservedMatErr);

        var matRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Test 22K Yellow Gold", "TEST_M6_GOLD", "Pure yellow gold", true, 1));
        var mat = await matRes.Content.ReadFromJsonAsync<MaterialDto>();

        var catBanglesRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Test Bangles", "TEST_M6_BGL", "Handcrafted bangles", true, 1));
        var catBangles = await catBanglesRes.Content.ReadFromJsonAsync<CategoryDto>();

        var catRingsRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Test Rings", "TEST_M6_RNG", "Cocktail & solitaire rings", true, 2));
        var catRings = await catRingsRes.Content.ReadFromJsonAsync<CategoryDto>();

        var catNecklacesRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Test Necklaces", "TEST_M6_NCK", "Traditional necklaces", true, 3));
        var catNecklaces = await catNecklacesRes.Content.ReadFromJsonAsync<CategoryDto>();

        var purRes = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("Test 22K Standard", "TEST_M6_22K", 22.0m, 91.67m, "Standard 22K jewellery", true, 1));
        var pur = await purRes.Content.ReadFromJsonAsync<PurityDto>();

        // 4. Create Physical Child Items for Testing
        _output.WriteLine("=== 4. Create Individual Inventory Items ===");
        // Bangle 1 (Branch 1, Loc 1, Category Bangles)
        var itemBgl1Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            b1.Id, loc1!.Id, "Test M6 Bangle Left", "TEST_M6_BGL_01", "TEST-M6-BGL-01", "BAR-M6-BGL-01",
            mat!.Id, catBangles!.Id, pur!.Id,
            12.5000m, 0.5000m, 1, 1, 5000.00m, "Left bangle of bridal pair", true, null, null
        ));
        Assert.Equal(HttpStatusCode.Created, itemBgl1Res.StatusCode);
        var itemBgl1 = await itemBgl1Res.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Bangle 2 (Branch 1, Loc 1, Category Bangles)
        var itemBgl2Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            b1.Id, loc1.Id, "Test M6 Bangle Right", "TEST_M6_BGL_02", "TEST-M6-BGL-02", "BAR-M6-BGL-02",
            mat.Id, catBangles.Id, pur.Id,
            12.8000m, 0.4000m, 1, 1, 5000.00m, "Right bangle of bridal pair", true, null, null
        ));
        Assert.Equal(HttpStatusCode.Created, itemBgl2Res.StatusCode);
        var itemBgl2 = await itemBgl2Res.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Bangle 3 (Branch 1, Loc 1, Category Bangles - for member add/detach test)
        var itemBgl3Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            b1.Id, loc1.Id, "Test M6 Bangle Center", "TEST_M6_BGL_03", "TEST-M6-BGL-03", "BAR-M6-BGL-03",
            mat.Id, catBangles.Id, pur.Id,
            15.0000m, 0.0000m, 1, 1, 6000.00m, "Center bangle", true, null, null
        ));
        Assert.Equal(HttpStatusCode.Created, itemBgl3Res.StatusCode);
        var itemBgl3 = await itemBgl3Res.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Ring 1 (Branch 1, Loc 1, Category Rings)
        var itemRng1Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            b1.Id, loc1.Id, "Test M6 Peacock Ring", "TEST_M6_RNG_01", "TEST-M6-RNG-01", "BAR-M6-RNG-01",
            mat.Id, catRings!.Id, pur.Id,
            6.2000m, 0.2000m, 1, 1, 3000.00m, "Peacock ring", true, null, null
        ));
        Assert.Equal(HttpStatusCode.Created, itemRng1Res.StatusCode);
        var itemRng1 = await itemRng1Res.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Necklace 1 (Branch 1, Loc 1, Category Necklaces)
        var itemNck1Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            b1.Id, loc1.Id, "Test M6 Peacock Choker", "TEST_M6_NCK_01", "TEST-M6-NCK-01", "BAR-M6-NCK-01",
            mat.Id, catNecklaces!.Id, pur.Id,
            35.0000m, 2.0000m, 1, 1, 15000.00m, "Peacock choker", true, null, null
        ));
        Assert.Equal(HttpStatusCode.Created, itemNck1Res.StatusCode);
        var itemNck1 = await itemNck1Res.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Cross-location item (Branch 1, Loc 1 Alt)
        var itemAltLocRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            b1.Id, loc1Alt!.Id, "Test M6 Vault Bangle", "TEST_M6_ALT_LOC", "TEST-M6-ALT-LOC", "BAR-M6-ALT-LOC",
            mat.Id, catBangles.Id, pur.Id,
            10.0000m, 0.0000m, 1, 1, 4000.00m, "Stored in safe", true, null, null
        ));
        Assert.Equal(HttpStatusCode.Created, itemAltLocRes.StatusCode);
        var itemAltLoc = await itemAltLocRes.Content.ReadFromJsonAsync<InventoryItemDto>();

        // Cross-branch item (Branch 2, Loc 2)
        var itemB2Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            b2.Id, loc2!.Id, "Test M6 Branch2 Bangle", "TEST_M6_B2_01", "TEST-M6-B2-01", "BAR-M6-B2-01",
            mat.Id, catBangles.Id, pur.Id,
            11.0000m, 0.0000m, 1, 1, 4000.00m, "Branch 2 stock", true, null, null
        ));
        Assert.Equal(HttpStatusCode.Created, itemB2Res.StatusCode);
        var itemB2 = await itemB2Res.Content.ReadFromJsonAsync<InventoryItemDto>();

        // 5. Test Assembly Creation: Rejection of Mixed-Category Items
        _output.WriteLine("=== 5. Assembly: Reject Mixed Categories ===");
        var invalidAssemblyReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Invalid Mixed Assembly",
            1, // Assembly
            "Should fail due to mixed categories",
            new List<Guid> { itemBgl1!.Id, itemRng1!.Id }
        );
        var invalidAssemblyRes = await ownerClient.PostAsJsonAsync("/api/compositions", invalidAssemblyReq);
        Assert.Equal(HttpStatusCode.BadRequest, invalidAssemblyRes.StatusCode);
        var invalidAssemblyErr = await invalidAssemblyRes.Content.ReadAsStringAsync();
        Assert.Contains("All child items in an Assembly must belong to the exact same category", invalidAssemblyErr);

        // 6. Test Assembly Creation: Reject Cross-Branch Items
        _output.WriteLine("=== 6. Reject Cross-Branch Children ===");
        var crossBranchReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Cross Branch Assembly",
            1, "Cross branch test",
            new List<Guid> { itemBgl1.Id, itemB2!.Id }
        );
        var crossBranchRes = await ownerClient.PostAsJsonAsync("/api/compositions", crossBranchReq);
        Assert.Equal(HttpStatusCode.BadRequest, crossBranchRes.StatusCode);
        var crossBranchErr = await crossBranchRes.Content.ReadAsStringAsync();
        Assert.Contains("belongs to a different branch", crossBranchErr);

        // 7. Test Assembly Creation: Reject Cross-Location Items
        _output.WriteLine("=== 7. Reject Cross-Location Children ===");
        var crossLocReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Cross Location Assembly",
            1, "Cross location test",
            new List<Guid> { itemBgl1.Id, itemAltLoc!.Id }
        );
        var crossLocRes = await ownerClient.PostAsJsonAsync("/api/compositions", crossLocReq);
        Assert.Equal(HttpStatusCode.BadRequest, crossLocRes.StatusCode);
        var crossLocErr = await crossLocRes.Content.ReadAsStringAsync();
        Assert.Contains("does not match composition location", crossLocErr);

        // 8. Test Assembly Creation: Reject Duplicate Children in Request
        _output.WriteLine("=== 8. Reject Duplicate Children in Request ===");
        var dupChildReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Duplicate Child Assembly",
            1, "Duplicate child test",
            new List<Guid> { itemBgl1.Id, itemBgl1.Id }
        );
        var dupChildRes = await ownerClient.PostAsJsonAsync("/api/compositions", dupChildReq);
        Assert.Equal(HttpStatusCode.BadRequest, dupChildRes.StatusCode);
        var dupChildErr = await dupChildRes.Content.ReadAsStringAsync();
        Assert.Contains("Duplicate child items are not allowed", dupChildErr);

        // 9. Test Assembly Creation: Reject Less Than 2 Children
        _output.WriteLine("=== 9. Reject Less Than 2 Children ===");
        var singleChildReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Single Child Assembly",
            1, "Single child test",
            new List<Guid> { itemBgl1.Id }
        );
        var singleChildRes = await ownerClient.PostAsJsonAsync("/api/compositions", singleChildReq);
        Assert.Equal(HttpStatusCode.BadRequest, singleChildRes.StatusCode);
        var singleChildErr = await singleChildRes.Content.ReadAsStringAsync();
        Assert.Contains("must contain at least 2 distinct existing inventory items", singleChildErr);

        // 10. Valid Assembly Creation: Automated Tag & Barcode Generation
        _output.WriteLine("=== 10. Valid Assembly Creation (Automated Identifiers) ===");
        var validAssemblyReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Sovereign Bangle Pair",
            1, // Assembly
            "Matching pair of handcrafted 22K bangles",
            new List<Guid> { itemBgl1.Id, itemBgl2!.Id }
        );
        var createAssemblyRes = await ownerClient.PostAsJsonAsync("/api/compositions", validAssemblyReq);
        Assert.Equal(HttpStatusCode.Created, createAssemblyRes.StatusCode);
        var assembly = await createAssemblyRes.Content.ReadFromJsonAsync<InventoryCompositionDto>();

        Assert.NotNull(assembly);
        Assert.Equal("Test M6 Sovereign Bangle Pair", assembly.Name);
        Assert.Equal(1, assembly.Type);
        Assert.Equal("ASSEMBLY", assembly.TypeName, ignoreCase: true);

        // Verify Automatic Namespace Separation:
        // Assembly Tag must be in reserved ASM namespace: {BRANCH}-ASM-{SEQ:D6}
        Assert.StartsWith($"{b1.Code}-ASM-", assembly.TagNumber);
        // Assembly Barcode must have reserved ASM prefix: ASM{BRANCH}{SEQ:D8}
        Assert.StartsWith($"ASM{b1.Code}", assembly.Barcode);

        Assert.Equal(1, assembly.Status); // Active
        Assert.Equal(2, assembly.ActiveMemberCount);

        // Verify Backend Calculated Totals:
        // itemBgl1: Gross=12.5000, Stone=0.5000, Net=12.0000, Pieces=1
        // itemBgl2: Gross=12.8000, Stone=0.4000, Net=12.4000, Pieces=1
        Assert.Equal(25.3000m, assembly.TotalGrossWeight);
        Assert.Equal(0.9000m, assembly.TotalStoneWeight);
        Assert.Equal(24.4000m, assembly.TotalNetWeight);
        Assert.Equal(2, assembly.TotalPieceCount);

        // Verify Child Records Remain Distinct in Database
        var getChild1Res = await ownerClient.GetAsync($"/api/inventory/{itemBgl1.Id}");
        Assert.Equal(HttpStatusCode.OK, getChild1Res.StatusCode);
        var child1After = await getChild1Res.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.Equal(1, child1After!.Status); // Still Available
        Assert.Equal(itemBgl1.TagNumber, child1After.TagNumber); // Tag preserved

        // 11. Constraint Test: Reject Adding an Already-Composed Child to Another Composition
        _output.WriteLine("=== 11. Reject Composing an Already-Composed Child ===");
        var duplicateComposeReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Attempt Steal Child",
            1, "Should fail because itemBgl1 is in another active composition",
            new List<Guid> { itemBgl1.Id, itemBgl3!.Id }
        );
        var duplicateComposeRes = await ownerClient.PostAsJsonAsync("/api/compositions", duplicateComposeReq);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateComposeRes.StatusCode);
        var duplicateComposeErr = await duplicateComposeRes.Content.ReadAsStringAsync();
        Assert.Contains("already part of another active composition", duplicateComposeErr);

        // 12. Database-Level Constraint Verification: Partial Unique Index
        _output.WriteLine("=== 12. Database Storage Engine Unique Index Enforcement ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Attempt to directly bypass service layer and insert duplicate active composition member in EF Core
            var rogueComp = new InventoryComposition
            {
                BranchId = b1.Id,
                StockLocationId = loc1.Id,
                Name = "Test M6 Rogue DB Comp",
                Type = Saranga.Domain.Enums.CompositionType.Assembly,
                TagNumber = $"{b1.Code}-ASM-999999",
                Barcode = $"ASM{b1.Code}99999999",
                Description = "Direct DB insert test"
            };
            db.InventoryCompositions.Add(rogueComp);
            await db.SaveChangesAsync();

            // Insert member with itemBgl1 which already has an active member row in assembly
            var rogueMember = new InventoryCompositionMember
            {
                CompositionId = rogueComp.Id,
                InventoryItemId = itemBgl1.Id,
                SortOrder = 1,
                IsActive = true
            };
            db.InventoryCompositionMembers.Add(rogueMember);

            // Must throw DbUpdateException due to Postgres unique partial index constraint IX_InventoryCompositionMembers_InventoryItemId
            var dbEx = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("IX_InventoryCompositionMembers_InventoryItemId", dbEx.InnerException?.Message ?? dbEx.Message);

            // Clean up rogue composition
            db.InventoryCompositions.Remove(rogueComp);
            await db.SaveChangesAsync();
        }

        // 13. Valid Group Creation: Automated Tag & Barcode Generation
        _output.WriteLine("=== 13. Valid Group Creation (Automated GRP Identifiers) ===");
        var validGroupReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Royal Bridal Set",
            2, // Group
            "Bridal choker with matching peacock ring",
            new List<Guid> { itemNck1!.Id, itemRng1!.Id }
        );
        var createGroupRes = await ownerClient.PostAsJsonAsync("/api/compositions", validGroupReq);
        Assert.Equal(HttpStatusCode.Created, createGroupRes.StatusCode);
        var group = await createGroupRes.Content.ReadFromJsonAsync<InventoryCompositionDto>();

        Assert.NotNull(group);
        Assert.Equal("Test M6 Royal Bridal Set", group.Name);
        Assert.Equal(2, group.Type);
        Assert.Equal("GROUP", group.TypeName, ignoreCase: true);

        // Verify Group Tag & Barcode namespace separation:
        Assert.StartsWith($"{b1.Code}-GRP-", group.TagNumber);
        Assert.StartsWith($"GRP{b1.Code}", group.Barcode);

        // itemNck1: Gross=35.0000, Stone=2.0000, Net=33.0000, Pieces=1
        // itemRng1: Gross=6.2000, Stone=0.2000, Net=6.0000, Pieces=1
        Assert.Equal(41.2000m, group.TotalGrossWeight);
        Assert.Equal(2.2000m, group.TotalStoneWeight);
        Assert.Equal(39.0000m, group.TotalNetWeight);
        Assert.Equal(2, group.TotalPieceCount);

        // 14. Composition Lookup by Tag and by Barcode
        _output.WriteLine("=== 14. Lookup Composition by Tag and by Barcode ===");
        var lookupTagRes = await ownerClient.GetAsync($"/api/compositions/lookup/tag/{assembly.TagNumber}");
        Assert.Equal(HttpStatusCode.OK, lookupTagRes.StatusCode);
        var lookupTagDto = await lookupTagRes.Content.ReadFromJsonAsync<InventoryCompositionDto>();
        Assert.Equal(assembly.Id, lookupTagDto!.Id);

        var lookupBarcodeRes = await ownerClient.GetAsync($"/api/compositions/lookup/barcode/{assembly.Barcode}");
        Assert.Equal(HttpStatusCode.OK, lookupBarcodeRes.StatusCode);
        var lookupBarcodeDto = await lookupBarcodeRes.Content.ReadFromJsonAsync<InventoryCompositionDto>();
        Assert.Equal(assembly.Id, lookupBarcodeDto!.Id);

        // 15. Optimistic Concurrency Control (409 Conflict)
        _output.WriteLine("=== 15. Optimistic Concurrency Control Verification ===");
        // Update group with stale row version -> Expect 409 Conflict
        var staleUpdateReq = new UpdateCompositionRequest("Stale Concurrent Update", "Should fail with 409", RowVersion: 999999);
        var staleUpdateRes = await ownerClient.PutAsJsonAsync($"/api/compositions/{group.Id}", staleUpdateReq);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdateRes.StatusCode);
        var conflictErr = await staleUpdateRes.Content.ReadAsStringAsync();
        Assert.Contains("modified concurrently by another transaction", conflictErr);

        // Update group with correct version -> Expect 200 OK
        var validUpdateReq = new UpdateCompositionRequest("Updated Royal Bridal Set", "Updated description", RowVersion: group.Version);
        var validUpdateRes = await ownerClient.PutAsJsonAsync($"/api/compositions/{group.Id}", validUpdateReq);
        Assert.Equal(HttpStatusCode.OK, validUpdateRes.StatusCode);
        var updatedGroup = await validUpdateRes.Content.ReadFromJsonAsync<InventoryCompositionDto>();
        Assert.Equal("Updated Royal Bridal Set", updatedGroup!.Name);

        // Dissolve with stale row version -> Expect 409 Conflict
        var staleDissolveReq = new DissolveCompositionRequest(RowVersion: 999999);
        var staleDissolveRes = await ownerClient.PostAsJsonAsync($"/api/compositions/{group.Id}/dissolve", staleDissolveReq);
        Assert.Equal(HttpStatusCode.Conflict, staleDissolveRes.StatusCode);

        // 16. Dynamic Member Add & Remove
        _output.WriteLine("=== 16. Dynamic Member Add and Remove ===");
        // Add itemBgl3 to Assembly (same category Bangles)
        var addBgl3Res = await ownerClient.PostAsJsonAsync($"/api/compositions/{assembly.Id}/members", new AddCompositionMemberRequest(itemBgl3.Id, 3));
        Assert.Equal(HttpStatusCode.OK, addBgl3Res.StatusCode);
        var assembly3Members = await addBgl3Res.Content.ReadFromJsonAsync<InventoryCompositionDto>();
        Assert.Equal(3, assembly3Members!.ActiveMemberCount);
        Assert.Equal(40.3000m, assembly3Members.TotalGrossWeight);
        Assert.Equal(3, assembly3Members.TotalPieceCount);

        // 17. Atomic POS Detachment Domain Operation Verification
        _output.WriteLine("=== 17. Atomic Future POS Detachment Operation ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var compService = scope.ServiceProvider.GetRequiredService<ICompositionService>();

            // Detach member 1 (itemBgl1) from 3-member assembly:
            // Remaining count = 2 -> Composition MUST remain ACTIVE
            var detach1Result = await compService.DetachMemberOnSaleAsync(itemBgl1.Id, "INV-2026-TEST-001");
            Assert.NotNull(detach1Result);
            Assert.Equal(3, detach1Result.PreviousActiveMemberCount);
            Assert.Equal(2, detach1Result.RemainingActiveMemberCount);
            Assert.False(detach1Result.CompositionWasDissolved);
            Assert.Equal("ACTIVE", detach1Result.StatusName);

            // Detach member 2 (itemBgl2) from now 2-member assembly:
            // Remaining count = 1 (< 2) -> Composition MUST AUTOMATICALLY DISSOLVE
            var detach2Result = await compService.DetachMemberOnSaleAsync(itemBgl2.Id, "INV-2026-TEST-002");
            Assert.NotNull(detach2Result);
            Assert.Equal(2, detach2Result.PreviousActiveMemberCount);
            Assert.Equal(0, detach2Result.RemainingActiveMemberCount);
            Assert.True(detach2Result.CompositionWasDissolved);
            Assert.Equal("DISSOLVED", detach2Result.StatusName);

            // Solitary remaining item (itemBgl3) must now be free:
            // Calling DetachMemberOnSale on itemBgl3 returns null (not in any active composition)
            var detach3Result = await compService.DetachMemberOnSaleAsync(itemBgl3.Id);
            Assert.Null(detach3Result);
        }

        // 18. Manual Dissolve on Group Composition
        _output.WriteLine("=== 18. Manual Dissolve Composition ===");
        var dissolveRes = await ownerClient.PostAsJsonAsync<DissolveCompositionRequest?>($"/api/compositions/{group.Id}/dissolve", null);
        Assert.Equal(HttpStatusCode.OK, dissolveRes.StatusCode);
        var dissolvedComp = await dissolveRes.Content.ReadFromJsonAsync<InventoryCompositionDto>();
        Assert.Equal(2, dissolvedComp!.Status); // Dissolved
        Assert.False(dissolvedComp.IsActive);

        // Verify Child Items are Intact and Re-Available for a brand new composition
        var recomposeReq = new CreateCompositionRequest(
            b1.Id, loc1.Id, "Test M6 Recomposed Assembly",
            1, "Recomposed after previous dissolution",
            new List<Guid> { itemBgl1.Id, itemBgl3.Id }
        );
        var recomposeRes = await ownerClient.PostAsJsonAsync("/api/compositions", recomposeReq);
        Assert.Equal(HttpStatusCode.Created, recomposeRes.StatusCode);

        // 19. RBAC & Branch Scoped Authorization
        _output.WriteLine("=== 19. RBAC & Branch Authorization ===");
        // Create Cashier Role with only InventoryView (no Composition Dissolve or Composition Create)
        var cashierRoleRes = await ownerClient.PostAsJsonAsync("/api/roles", new CreateRoleRequest("Test M6 Cashier Role", "Cashier read only", new List<Guid>()));
        var cashierRole = await cashierRoleRes.Content.ReadFromJsonAsync<RoleDto>();

        // Create Cashier User assigned ONLY to Branch 2
        var cashierUserRes = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "test_cashier_m6", "cashier_m6@saranga.lk", "CashierM6@2026#Secure", "Test Cashier M6",
            new List<Guid> { cashierRole!.Id },
            new List<Guid> { b2.Id },
            b2.Id, false
        ));
        var cashierUser = await cashierUserRes.Content.ReadFromJsonAsync<UserDto>();

        // Log in as Cashier
        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_cashier_m6", "CashierM6@2026#Secure"));
        var cashierAuth = await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        var cashierClient = CreateClientWithCookieHandler();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierAuth!.AccessToken);

        // Cashier attempts to dissolve Branch 1 composition -> Forbidden (missing INVENTORY_COMPOSITION_DISSOLVE)
        var cashierDissolveRes = await cashierClient.PostAsJsonAsync<DissolveCompositionRequest?>($"/api/compositions/{group.Id}/dissolve", null);
        Assert.Equal(HttpStatusCode.Forbidden, cashierDissolveRes.StatusCode);

        // 20. Audit Trail Verification
        _output.WriteLine("=== 20. Audit Trail Verification ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var compAudit = await db.AuditLogs
                .Where(a => a.Action == "COMPOSITION_CREATE" ||
                            a.Action == "MEMBER_ADDED" ||
                            a.Action == "MEMBER_REMOVED" ||
                            a.Action == "COMPOSITION_DISSOLVED" ||
                            a.Action == "MEMBER_DETACHED_SALE" ||
                            a.Action == "COMPOSITION_AUTO_DISSOLVED")
                .ToListAsync();

            Assert.Contains(compAudit, a => a.Action == "COMPOSITION_CREATE");
            Assert.Contains(compAudit, a => a.Action == "MEMBER_ADDED");
            Assert.Contains(compAudit, a => a.Action == "COMPOSITION_DISSOLVED");
            Assert.Contains(compAudit, a => a.Action == "MEMBER_DETACHED_SALE");
            Assert.Contains(compAudit, a => a.Action == "COMPOSITION_AUTO_DISSOLVED");
        }

        _output.WriteLine("=== ALL HARDENED MODULE 6 TESTS COMPLETED SUCCESSFULLY ===");
    }
}

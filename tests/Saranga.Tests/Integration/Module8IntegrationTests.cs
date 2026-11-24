using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
using Saranga.Application.DTOs.InventoryCount;
using Saranga.Application.DTOs.Roles;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module8IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Module8IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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
            DELETE FROM ""InventoryCountUnexpectedItems"" WHERE ""InventoryCountId"" IN (
                SELECT ""Id"" FROM ""InventoryCounts"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T8B1', 'T8B2'))
            );
            DELETE FROM ""InventoryCountLines"" WHERE ""InventoryCountId"" IN (
                SELECT ""Id"" FROM ""InventoryCounts"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T8B1', 'T8B2'))
            );
            DELETE FROM ""InventoryCounts"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T8B1', 'T8B2'));

            DELETE FROM ""InventoryCompositionMembers"" WHERE ""CompositionId"" IN (
                SELECT ""Id"" FROM ""InventoryCompositions"" WHERE ""TagNumber"" LIKE 'ASM-T8B1-%' OR ""Name"" LIKE 'Test M8%'
            );
            DELETE FROM ""InventoryCompositions"" WHERE ""TagNumber"" LIKE 'ASM-T8B1-%' OR ""Name"" LIKE 'Test M8%';

            DELETE FROM ""InventoryItemSpecifications"" WHERE ""InventoryItemId"" IN (
                SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M8_%' OR ""TagNumber"" LIKE 'T8B1-%' OR ""TagNumber"" LIKE 'T8B2-%' OR ""Name"" LIKE 'Test M8%'
            );
            DELETE FROM ""InventoryItemMedia"" WHERE ""InventoryItemId"" IN (
                SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M8_%' OR ""TagNumber"" LIKE 'T8B1-%' OR ""TagNumber"" LIKE 'T8B2-%' OR ""Name"" LIKE 'Test M8%'
            );
            DELETE FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M8_%' OR ""TagNumber"" LIKE 'T8B1-%' OR ""TagNumber"" LIKE 'T8B2-%' OR ""Name"" LIKE 'Test M8%';

            DELETE FROM ""StockLocations"" WHERE ""Code"" LIKE 'TEST_M8_%';
            DELETE FROM ""Materials"" WHERE ""Code"" LIKE 'TEST_M8_%';
            DELETE FROM ""Categories"" WHERE ""Code"" LIKE 'TEST_M8_%';
            DELETE FROM ""Purities"" WHERE ""Code"" LIKE 'TEST_M8_%';

            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T8B1', 'T8B2'))
                OR ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m8_%');
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m8_%');
            DELETE FROM ""Users"" WHERE ""Username"" LIKE 'test_m8_%';
            DELETE FROM ""Branches"" WHERE ""Code"" IN ('T8B1', 'T8B2');
        ");
    }

    [Fact]
    public async Task ComprehensiveModule8_InventoryCount_EndToEndVerification()
    {
        var client = _factory.CreateClient();

        // =========================================================================
        // 1. Authenticate as Owner / Super Admin
        // =========================================================================
        _output.WriteLine("=== 1. Authenticate as Owner / Super Admin ===");
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            Username: "saranga_owner",
            Password: "OwnerSuperAdmin@2026#Secure"
        ));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginData = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginData);
        var ownerToken = loginData.AccessToken;

        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // =========================================================================
        // 2. Setup Test Branches & Stock Locations
        // =========================================================================
        _output.WriteLine("=== 2. Setup Test Branches & Stock Locations ===");
        var b1Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test Branch M8 Jaffna",
            Code: "T8B1",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Created, b1Res.StatusCode);
        var b1 = (await b1Res.Content.ReadFromJsonAsync<BranchDto>())!;

        var b2Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test Branch M8 Kandy",
            Code: "T8B2",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Created, b2Res.StatusCode);
        var b2 = (await b2Res.Content.ReadFromJsonAsync<BranchDto>())!;

        // Locations in B1
        var loc1Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(
            BranchId: b1.Id,
            Name: "Test M8 Vault Main",
            Code: "TEST_M8_LOC1",
            LocationType: "VAULT",
            Description: "Secure counting vault",
            IsActive: true,
            SortOrder: 1
        ));
        Assert.Equal(HttpStatusCode.Created, loc1Res.StatusCode);
        var loc1 = (await loc1Res.Content.ReadFromJsonAsync<StockLocationDto>())!;

        var loc2Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(
            BranchId: b1.Id,
            Name: "Test M8 Display Counter",
            Code: "TEST_M8_LOC2",
            LocationType: "DISPLAY",
            Description: "Showcase counter",
            IsActive: true,
            SortOrder: 2
        ));
        Assert.Equal(HttpStatusCode.Created, loc2Res.StatusCode);
        var loc2 = (await loc2Res.Content.ReadFromJsonAsync<StockLocationDto>())!;

        // Location in B2 (for cross-branch check)
        var locB2Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(
            BranchId: b2.Id,
            Name: "Test M8 Kandy Vault",
            Code: "TEST_M8_B2LOC",
            LocationType: "VAULT",
            Description: "Kandy vault",
            IsActive: true,
            SortOrder: 1
        ));
        Assert.Equal(HttpStatusCode.Created, locB2Res.StatusCode);
        var locB2 = (await locB2Res.Content.ReadFromJsonAsync<StockLocationDto>())!;

        // =========================================================================
        // 3. Setup Master Data
        // =========================================================================
        _output.WriteLine("=== 3. Setup Master Data ===");
        var matRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Test M8 Gold", "TEST_M8_GOLD", "Test gold", true, 1));
        var catRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Test M8 Bangles", "TEST_M8_BGL", "Test bangles", true, 1));
        var purRes = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("Test M8 22K", "TEST_M8_22K", 22.0m, 91.67m, "Standard 22K", true, 1));

        var mat = (await matRes.Content.ReadFromJsonAsync<MaterialDto>())!;
        var cat = (await catRes.Content.ReadFromJsonAsync<CategoryDto>())!;
        var pur = (await purRes.Content.ReadFromJsonAsync<PurityDto>())!;

        // =========================================================================
        // 4. Create Physical Jewellery Items
        // =========================================================================
        _output.WriteLine("=== 4. Create Physical Jewellery Items ===");

        // Item 1: Available in LOC1
        var item1Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test M8 Single Bangle 1",
            ItemCode: "TEST_M8_01",
            TagNumber: "T8B1-BGL-000001",
            Barcode: "BAR-T8B1-000001",
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 12.5000m,
            StoneWeight: 0.5000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 1500m
        ));
        Assert.Equal(HttpStatusCode.Created, item1Res.StatusCode);
        var item1 = (await item1Res.Content.ReadFromJsonAsync<InventoryItemDto>())!;

        // Item 2: Reserved in LOC1 (MUST be included in physical baseline!)
        var item2Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test M8 Reserved Bangle 2",
            ItemCode: "TEST_M8_02",
            TagNumber: "T8B1-BGL-000002",
            Barcode: "BAR-T8B1-000002",
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 15.0000m,
            StoneWeight: 0.0000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 2000m
        ));
        Assert.Equal(HttpStatusCode.Created, item2Res.StatusCode);
        var item2 = (await item2Res.Content.ReadFromJsonAsync<InventoryItemDto>())!;

        // Mark Item 2 as Reserved in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbItem2 = await db.InventoryItems.FindAsync(item2.Id);
            dbItem2!.Status = InventoryStatus.Reserved;
            await db.SaveChangesAsync();
        }

        // Item 3: Sold in LOC1 (MUST be EXCLUDED from baseline!)
        var item3Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test M8 Sold Bangle 3",
            ItemCode: "TEST_M8_03",
            TagNumber: "T8B1-BGL-000003",
            Barcode: "BAR-T8B1-000003",
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 8.0000m,
            StoneWeight: 0.0000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 1000m
        ));
        Assert.Equal(HttpStatusCode.Created, item3Res.StatusCode);
        var item3 = (await item3Res.Content.ReadFromJsonAsync<InventoryItemDto>())!;

        // Mark Item 3 as Sold in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbItem3 = await db.InventoryItems.FindAsync(item3.Id);
            dbItem3!.Status = InventoryStatus.Sold;
            await db.SaveChangesAsync();
        }

        // Item 4 & 5: Available in LOC1, composed into an Assembly
        var item4Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test M8 Child Item 4",
            ItemCode: "TEST_M8_04",
            TagNumber: "T8B1-BGL-000004",
            Barcode: "BAR-T8B1-000004",
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 10.0000m,
            StoneWeight: 0.0000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 1200m
        ));
        var item5Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test M8 Child Item 5",
            ItemCode: "TEST_M8_05",
            TagNumber: "T8B1-BGL-000005",
            Barcode: "BAR-T8B1-000005",
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 10.0000m,
            StoneWeight: 0.0000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 1200m
        ));
        var item4 = (await item4Res.Content.ReadFromJsonAsync<InventoryItemDto>())!;
        var item5 = (await item5Res.Content.ReadFromJsonAsync<InventoryItemDto>())!;

        // Compose Assembly ASM-1 containing Item 4 and Item 5
        var asmRes = await ownerClient.PostAsJsonAsync("/api/compositions", new CreateCompositionRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test M8 Assembly Bangles Pair",
            Type: 1, // Assembly
            Description: "Two bangles assembled together",
            ChildItemIds: new List<Guid> { item4.Id, item5.Id }
        ));
        Assert.Equal(HttpStatusCode.Created, asmRes.StatusCode);
        var asm = (await asmRes.Content.ReadFromJsonAsync<InventoryCompositionDto>())!;

        // Item 6: In LOC2 (same branch, wrong location -> will be UNEXPECTED if scanned during LOC1 count)
        var item6Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc2.Id,
            Name: "Test M8 Counter Item 6",
            ItemCode: "TEST_M8_06",
            TagNumber: "T8B1-BGL-000006",
            Barcode: "BAR-T8B1-000006",
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 5.5000m,
            StoneWeight: 0.0000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 800m
        ));
        var item6 = (await item6Res.Content.ReadFromJsonAsync<InventoryItemDto>())!;

        // Item 7: In Branch 2 (cross-branch item)
        var item7Res = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b2.Id,
            StockLocationId: locB2.Id,
            Name: "Test M8 Kandy Item 7",
            ItemCode: "TEST_M8_07",
            TagNumber: "T8B2-BGL-000007",
            Barcode: "BAR-T8B2-000007",
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 7.0000m,
            StoneWeight: 0.0000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 900m
        ));
        var item7 = (await item7Res.Content.ReadFromJsonAsync<InventoryItemDto>())!;

        // =========================================================================
        // 5. Start Inventory Count & Verify Frozen Baseline
        // =========================================================================
        _output.WriteLine("=== 5. Start Inventory Count & Verify Frozen Baseline ===");
        var startRes = await ownerClient.PostAsJsonAsync("/api/inventory-counts", new StartInventoryCountRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Notes: "Module 8 Integration Verification Count"
        ));
        Assert.Equal(HttpStatusCode.Created, startRes.StatusCode);
        var countSession = (await startRes.Content.ReadFromJsonAsync<InventoryCountDto>())!;

        Assert.Equal(b1.Id, countSession.BranchId);
        Assert.Equal(loc1.Id, countSession.StockLocationId);
        Assert.Equal(1, countSession.Status); // InProgress
        Assert.Equal("IN_PROGRESS", countSession.StatusName);

        // Core Accounting Invariant Verification:
        // Expected physical items MUST be: Item 1 (Available), Item 2 (Reserved), Item 4 (Child), Item 5 (Child) = Exactly 4 items!
        // Item 3 (SOLD) is excluded.
        // Assembly parent asm is NOT an extra stock line!
        Assert.Equal(4, countSession.ExpectedItemCount);
        Assert.Equal(0, countSession.CountedExpectedItemCount);
        Assert.Equal(0, countSession.MatchedCount);
        Assert.Equal(0, countSession.MissingCount);
        Assert.Equal(0, countSession.UnexpectedCount);

        // Verify count lines in database
        var linesRes = await ownerClient.GetFromJsonAsync<PagedResult<InventoryCountLineDto>>($"/api/inventory-counts/{countSession.Id}/lines");
        Assert.NotNull(linesRes);
        Assert.Equal(4, linesRes.TotalCount);

        var baselineItemIds = linesRes.Items.Select(l => l.InventoryItemId).ToList();
        Assert.Contains(item1.Id, baselineItemIds);
        Assert.Contains(item2.Id, baselineItemIds);
        Assert.Contains(item4.Id, baselineItemIds);
        Assert.Contains(item5.Id, baselineItemIds);
        Assert.DoesNotContain(item3.Id, baselineItemIds); // SOLD excluded

        // Verify frozen weight snapshot precision (numeric 18,4)
        var line1 = linesRes.Items.First(l => l.InventoryItemId == item1.Id);
        Assert.Equal(12.5000m, line1.GrossWeightSnapshot);
        Assert.Equal(0.5000m, line1.StoneWeightSnapshot);
        Assert.Equal(12.0000m, line1.NetWeightSnapshot);
        Assert.False(line1.IsCounted);
        Assert.Equal(0, line1.Result); // Pending

        // =========================================================================
        // 6. Enforce One Active Count Per Location (Database & Service constraint)
        // =========================================================================
        _output.WriteLine("=== 6. Enforce One Active Count Per Location ===");
        var duplicateStartRes = await ownerClient.PostAsJsonAsync("/api/inventory-counts", new StartInventoryCountRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Notes: "Illegal duplicate count attempt"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateStartRes.StatusCode);
        var dupBody = await duplicateStartRes.Content.ReadAsStringAsync();
        Assert.Contains("An active inventory count is already in progress", dupBody);

        // =========================================================================
        // 7. Scanning Expected Items & Duplicate Scan Protection
        // =========================================================================
        _output.WriteLine("=== 7. Scanning Expected Items & Duplicate Scan Protection ===");

        // Scan Item 1 by Barcode
        var scan1Res = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(
            Identifier: item1.Barcode
        ));
        Assert.Equal(HttpStatusCode.OK, scan1Res.StatusCode);
        var scan1Data = (await scan1Res.Content.ReadFromJsonAsync<ScanResultDto>())!;
        Assert.Equal("MATCHED", scan1Data.ScanOutcome);
        Assert.NotNull(scan1Data.ExpectedLine);
        Assert.True(scan1Data.ExpectedLine.IsCounted);
        Assert.Equal(1, scan1Data.CountSummary.CountedExpectedItemCount);
        Assert.Equal(25.0m, scan1Data.CountSummary.ProgressPercentage); // 1 / 4 = 25%

        // Duplicate Scan: Scan Item 1 again by Tag Number
        var dupScan1Res = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(
            Identifier: item1.TagNumber
        ));
        Assert.Equal(HttpStatusCode.OK, dupScan1Res.StatusCode);
        var dupScan1Data = (await dupScan1Res.Content.ReadFromJsonAsync<ScanResultDto>())!;
        Assert.Equal("ALREADY_COUNTED", dupScan1Data.ScanOutcome);
        Assert.Contains("was already counted", dupScan1Data.Message);
        Assert.Equal(1, dupScan1Data.CountSummary.CountedExpectedItemCount); // Unchanged!

        // Scan Item 2 (Reserved item) by Tag
        var scan2Res = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(
            Identifier: item2.TagNumber
        ));
        Assert.Equal(HttpStatusCode.OK, scan2Res.StatusCode);
        var scan2Data = (await scan2Res.Content.ReadFromJsonAsync<ScanResultDto>())!;
        Assert.Equal("MATCHED", scan2Data.ScanOutcome);
        Assert.Equal(2, scan2Data.CountSummary.CountedExpectedItemCount);
        Assert.Equal(50.0m, scan2Data.CountSummary.ProgressPercentage); // 2 / 4 = 50%

        // =========================================================================
        // 8. Concurrent Duplicate Scan Protection
        // =========================================================================
        _output.WriteLine("=== 8. Concurrent Duplicate Scan Protection ===");
        var taskA = ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(item2.Barcode));
        var taskB = ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(item2.TagNumber));
        var responses = await Task.WhenAll(taskA, taskB);

        var resA = await responses[0].Content.ReadFromJsonAsync<ScanResultDto>();
        var resB = await responses[1].Content.ReadFromJsonAsync<ScanResultDto>();
        Assert.NotNull(resA);
        Assert.NotNull(resB);
        Assert.Equal("ALREADY_COUNTED", resA.ScanOutcome);
        Assert.Equal("ALREADY_COUNTED", resB.ScanOutcome);

        // =========================================================================
        // 9. Unexpected Item (Same branch, different location)
        // =========================================================================
        _output.WriteLine("=== 9. Unexpected Item Handling ===");
        // Item 6 is in LOC2, but scanned during LOC1 count
        var unexpRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(
            Identifier: item6.Barcode
        ));
        Assert.Equal(HttpStatusCode.OK, unexpRes.StatusCode);
        var unexpData = (await unexpRes.Content.ReadFromJsonAsync<ScanResultDto>())!;
        Assert.Equal("UNEXPECTED", unexpData.ScanOutcome);
        Assert.NotNull(unexpData.UnexpectedItem);
        Assert.Equal(item6.Id, unexpData.UnexpectedItem.InventoryItemId);
        Assert.Equal(1, unexpData.CountSummary.UnexpectedCount);

        // Core Invariant Verification: Live InventoryItem.StockLocationId was NOT silently modified
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item6Db = await db.InventoryItems.FindAsync(item6.Id);
            Assert.Equal(loc2.Id, item6Db!.StockLocationId); // Still in LOC2!
        }

        // =========================================================================
        // 10. Cross-Branch Item Handling
        // =========================================================================
        _output.WriteLine("=== 10. Cross-Branch Item Handling ===");
        // Item 7 is in Branch 2 (Kandy)
        var crossBranchRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(
            Identifier: item7.Barcode
        ));
        Assert.Equal(HttpStatusCode.OK, crossBranchRes.StatusCode);
        var crossData = (await crossBranchRes.Content.ReadFromJsonAsync<ScanResultDto>())!;
        Assert.Equal("CROSS_BRANCH", crossData.ScanOutcome);
        Assert.Contains("belongs to another branch", crossData.Message);
        // Not added to unexpected items or baseline
        Assert.Equal(1, crossData.CountSummary.UnexpectedCount);

        // =========================================================================
        // 11. Assembly Parent Scan & Partial Member Confirmation
        // =========================================================================
        _output.WriteLine("=== 11. Assembly Parent Scan & Partial Member Confirmation ===");
        // Scanning parent Assembly Barcode
        var parentScanRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(
            Identifier: asm.Barcode
        ));
        Assert.Equal(HttpStatusCode.OK, parentScanRes.StatusCode);
        var parentScanData = (await parentScanRes.Content.ReadFromJsonAsync<ScanResultDto>())!;
        Assert.Equal("COMPOSITION_PARENT", parentScanData.ScanOutcome);
        Assert.NotNull(parentScanData.CompositionPreview);
        Assert.Equal(2, parentScanData.CompositionPreview.Members.Count);

        // Confirm partial physical presence: ONLY Item 4 is present, Item 5 is missing!
        var confirmRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan-composition", new ConfirmCompositionScanRequest(
            CompositionId: asm.Id,
            SelectedInventoryItemIds: new List<Guid> { item4.Id } // Only item 4!
        ));
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var confirmData = (await confirmRes.Content.ReadFromJsonAsync<ConfirmCompositionScanResultDto>())!;
        Assert.Equal(1, confirmData.NewlyCountedCount);
        Assert.Equal(3, confirmData.CountSummary.CountedExpectedItemCount); // Items 1, 2, 4 counted

        // Child already counted then parent scanned
        var parentScan2Res = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/scan", new ScanInventoryItemRequest(
            Identifier: asm.TagNumber
        ));
        var parentScan2Data = (await parentScan2Res.Content.ReadFromJsonAsync<ScanResultDto>())!;
        var member4Preview = parentScan2Data.CompositionPreview!.Members.First(m => m.InventoryItemId == item4.Id);
        Assert.True(member4Preview.IsAlreadyCounted);

        // =========================================================================
        // 12. Frozen Baseline Immutable to Live Item Edits
        // =========================================================================
        _output.WriteLine("=== 12. Frozen Baseline Immutable to Live Item Edits ===");
        // Edit Item 1 weight in live system from 12.5g to 18.0g
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbItem1 = await db.InventoryItems.FindAsync(item1.Id);
            dbItem1!.GrossWeight = 18.0000m;
            await db.SaveChangesAsync();
        }

        // Verify baseline snapshot still has 12.5000g!
        var snapshotLine1 = (await ownerClient.GetFromJsonAsync<PagedResult<InventoryCountLineDto>>($"/api/inventory-counts/{countSession.Id}/lines"))!
            .Items.First(l => l.InventoryItemId == item1.Id);
        Assert.Equal(12.5000m, snapshotLine1.GrossWeightSnapshot);

        // =========================================================================
        // 13. Finish / Reconciliation & Missing Detection
        // =========================================================================
        _output.WriteLine("=== 13. Finish / Reconciliation & Missing Detection ===");
        // Finish count: Item 5 was never counted (missing), and Item 6 was unexpected.
        var finishRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/finish", new { });
        Assert.Equal(HttpStatusCode.OK, finishRes.StatusCode);
        var reconciledSession = (await finishRes.Content.ReadFromJsonAsync<InventoryCountDto>())!;

        Assert.Equal(2, reconciledSession.Status); // RequiresReview
        Assert.Equal("REQUIRES_REVIEW", reconciledSession.StatusName);
        Assert.Equal(3, reconciledSession.MatchedCount); // 1, 2, 4
        Assert.Equal(1, reconciledSession.MissingCount); // 5
        Assert.Equal(1, reconciledSession.UnexpectedCount); // 6
        Assert.NotNull(reconciledSession.ReconciledAt);

        // Verify Item 5 was marked Missing in lines
        var variances = await ownerClient.GetFromJsonAsync<CountVariancesDto>($"/api/inventory-counts/{countSession.Id}/variances");
        Assert.NotNull(variances);
        Assert.Single(variances.MissingItems);
        Assert.Equal(item5.Id, variances.MissingItems[0].InventoryItemId);
        Assert.Equal(2, variances.MissingItems[0].Result); // Missing
        Assert.Equal(0, variances.MissingItems[0].ResolutionStatus); // Unresolved

        Assert.Single(variances.UnexpectedItems);
        Assert.Equal(item6.Id, variances.UnexpectedItems[0].InventoryItemId);
        Assert.Equal(0, variances.UnexpectedItems[0].ResolutionStatus); // Unresolved

        // =========================================================================
        // 14. Unauthorized Variance Resolution Enforcement
        // =========================================================================
        _output.WriteLine("=== 14. Unauthorized Variance Resolution Enforcement ===");
        var cashierUsername = $"test_m8_cashier_{Guid.NewGuid():N}"[..20];
        var cashierEmail = $"{cashierUsername}@saranga.test";
        // Create Cashier user (who lacks INVENTORY_VARIANCE_RESOLVE)
        var cashierUserRes = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: cashierUsername,
            Email: cashierEmail,
            Password: "CashierPassword@2026#",
            FullName: "Test M8 Cashier",
            DefaultBranchId: b1.Id,
            RoleIds: new List<Guid> { (await ownerClient.GetFromJsonAsync<List<RoleDto>>("/api/roles"))!.First(r => r.Name == "Cashier").Id },
            BranchIds: new List<Guid> { b1.Id }
        ));
        Assert.Equal(HttpStatusCode.Created, cashierUserRes.StatusCode);

        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(cashierUsername, "CashierPassword@2026#"));
        var cashierToken = (await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var cashierClient = _factory.CreateClient();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);

        // Cashier attempts to resolve variance -> MUST be 403 Forbidden!
        var unauthResolveRes = await cashierClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/variances/resolve", new ResolveVarianceRequest(
            VarianceType: "Missing",
            VarianceId: variances.MissingItems[0].Id,
            ReasonCode: "CONFIRMED_MISSING",
            Notes: "Illegal resolve attempt"
        ));
        Assert.Equal(HttpStatusCode.Forbidden, unauthResolveRes.StatusCode);

        // =========================================================================
        // 15. Attempting to Complete with Unresolved Variances (Must fail)
        // =========================================================================
        _output.WriteLine("=== 15. Attempting to Complete with Unresolved Variances ===");
        var prematureCompleteRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/complete", new CompleteInventoryCountRequest(null));
        Assert.Equal(HttpStatusCode.BadRequest, prematureCompleteRes.StatusCode);
        var prematureMsg = await prematureCompleteRes.Content.ReadAsStringAsync();
        Assert.Contains("all variances must be resolved first", prematureMsg);

        // =========================================================================
        // 16. Authoritative Variance Resolution (No Automatic Inventory Mutation)
        // =========================================================================
        _output.WriteLine("=== 16. Authoritative Variance Resolution ===");
        // 1. Resolve Missing Item 5
        var resolveMissingRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/variances/resolve", new ResolveVarianceRequest(
            VarianceType: "Missing",
            VarianceId: variances.MissingItems[0].Id,
            ReasonCode: "CONFIRMED_MISSING",
            Notes: "Stock shortage confirmed after physical search in showcase."
        ));
        Assert.Equal(HttpStatusCode.OK, resolveMissingRes.StatusCode);

        // 2. Resolve Unexpected Item 6
        var resolveUnexpRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/variances/resolve", new ResolveVarianceRequest(
            VarianceType: "Unexpected",
            VarianceId: variances.UnexpectedItems[0].Id,
            ReasonCode: "WRONG_RECORDED_LOCATION",
            Notes: "Item was placed in vault without transfer record update. Investigation logged."
        ));
        Assert.Equal(HttpStatusCode.OK, resolveUnexpRes.StatusCode);

        // CRITICAL INVARIANT VERIFICATION: Resolving variance did NOT modify Item 5 or Item 6!
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item5Db = await db.InventoryItems.FindAsync(item5.Id);
            var item6Db = await db.InventoryItems.FindAsync(item6.Id);

            // Item 5 is NOT deleted or deactivated merely because it was declared missing!
            Assert.True(item5Db!.IsActive);
            Assert.Equal(InventoryStatus.Available, item5Db.Status);

            // Item 6 StockLocationId was NOT silently moved to Vault!
            Assert.Equal(loc2.Id, item6Db!.StockLocationId);
        }

        // =========================================================================
        // 17. Successful Completion of Count Session
        // =========================================================================
        _output.WriteLine("=== 17. Successful Completion of Count Session ===");
        var completeRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/complete", new CompleteInventoryCountRequest(
            Notes: "All variances resolved with branch supervisor sign-off."
        ));
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);
        var completedCount = (await completeRes.Content.ReadFromJsonAsync<InventoryCountDto>())!;

        Assert.Equal(3, completedCount.Status); // Completed
        Assert.Equal("COMPLETED", completedCount.StatusName);
        Assert.NotNull(completedCount.CompletedAt);

        // Attempting to cancel a completed count must fail
        var cancelCompletedRes = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{countSession.Id}/cancel", new CancelInventoryCountRequest("Try cancel completed"));
        Assert.Equal(HttpStatusCode.BadRequest, cancelCompletedRes.StatusCode);

        // =========================================================================
        // 18. Perfect Match Auto-Completion
        // =========================================================================
        _output.WriteLine("=== 18. Perfect Match Auto-Completion ===");
        // Start count on loc2 (only contains Item 6)
        var startLoc2Res = await ownerClient.PostAsJsonAsync("/api/inventory-counts", new StartInventoryCountRequest(
            BranchId: b1.Id,
            StockLocationId: loc2.Id,
            Notes: "Loc 2 Perfect Count Test"
        ));
        Assert.Equal(HttpStatusCode.Created, startLoc2Res.StatusCode);
        var loc2Count = (await startLoc2Res.Content.ReadFromJsonAsync<InventoryCountDto>())!;
        Assert.Equal(1, loc2Count.ExpectedItemCount);

        // Scan Item 6
        var scanItem6Res = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{loc2Count.Id}/scan", new ScanInventoryItemRequest(item6.Barcode));
        Assert.Equal(HttpStatusCode.OK, scanItem6Res.StatusCode);

        // Finish count -> 0 Missing, 0 Unexpected -> MUST be automatically COMPLETED!
        var finishLoc2Res = await ownerClient.PostAsJsonAsync($"/api/inventory-counts/{loc2Count.Id}/finish", new { });
        Assert.Equal(HttpStatusCode.OK, finishLoc2Res.StatusCode);
        var loc2Finished = (await finishLoc2Res.Content.ReadFromJsonAsync<InventoryCountDto>())!;
        Assert.Equal(3, loc2Finished.Status); // Completed automatically!
        Assert.Equal("COMPLETED", loc2Finished.StatusName);

        // =========================================================================
        // 19. Audit Trail Verification
        // =========================================================================
        _output.WriteLine("=== 19. Audit Trail Verification ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audits = await db.AuditLogs
                .AsNoTracking()
                .Where(a => a.BranchId == b1.Id)
                .ToListAsync();

            var actions = audits.Select(a => a.Action).ToList();
            Assert.Contains("COUNT_STARTED", actions);
            Assert.Contains("UNEXPECTED_ITEM_FOUND", actions);
            Assert.Contains("COUNT_RECONCILED", actions);
            Assert.Contains("VARIANCE_RESOLVED", actions);
            Assert.Contains("COUNT_COMPLETED", actions);
        }

        _output.WriteLine("=== Module 8 Integration Verification Completed Successfully ===");
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Api;
using Saranga.Application.DTOs.Attributes;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.DTOs.Branches;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Inventory;
using Saranga.Application.DTOs.Roles;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module5IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public Module5IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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
            DELETE FROM ""IdempotencyRecords"" WHERE ""Key"" LIKE 'test-m5-%';
            DELETE FROM ""InventoryItemSpecifications"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M5_%' OR ""TagNumber"" LIKE 'TEST-M5-%');
            DELETE FROM ""InventoryItemMedia"" WHERE ""InventoryItemId"" IN (SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M5_%' OR ""TagNumber"" LIKE 'TEST-M5-%');
            DELETE FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M5_%' OR ""TagNumber"" LIKE 'TEST-M5-%' OR ""Name"" LIKE 'Test M5%';
            DELETE FROM ""StockLocations"" WHERE ""Code"" LIKE 'TEST_M5_%';
            DELETE FROM ""SpecificationOptions"" WHERE ""Code"" LIKE 'TEST_M5_%';
            DELETE FROM ""Specifications"" WHERE ""Code"" LIKE 'TEST_M5_%';
            DELETE FROM ""Purities"" WHERE ""Code"" LIKE 'TEST_M5_%';
            DELETE FROM ""Categories"" WHERE ""Code"" LIKE 'TEST_M5_%';
            DELETE FROM ""Materials"" WHERE ""Code"" LIKE 'TEST_M5_%';
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('TEST-M5-B1', 'TEST-M5-B2'));
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m5', 'test_editor_m5'));
            DELETE FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m5', 'test_editor_m5');
            DELETE FROM ""RolePermissions"" WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""Roles"" WHERE ""Name"" LIKE 'Test M5%');
            DELETE FROM ""Roles"" WHERE ""Name"" LIKE 'Test M5%';
            DELETE FROM ""Branches"" WHERE ""Code"" IN ('TEST-M5-B1', 'TEST-M5-B2');
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
    public async Task ComprehensiveModule5_ManualItemCreation_EndToEndVerification()
    {
        var client = CreateClientWithCookieHandler();

        // 1. Authenticate as Owner / Super Admin
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var ownerToken = authData!.AccessToken;

        var ownerClient = CreateClientWithCookieHandler();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // 2. Setup Prerequisites: Branches & Locations
        _output.WriteLine("=== Setting up test branches and stock locations ===");
        var b1Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M5 Branch 1", "TEST-M5-B1", "LK", "LKR", "Asia/Colombo", true));
        var b1 = await b1Res.Content.ReadFromJsonAsync<BranchDto>();

        var b2Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M5 Branch 2", "TEST-M5-B2", "LK", "LKR", "Asia/Colombo", true));
        var b2 = await b2Res.Content.ReadFromJsonAsync<BranchDto>();

        var loc1Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1!.Id, "Main Display B1", "TEST_M5_LOC_1", "DISPLAY", "B1 Display Showcase", true, 1));
        var loc1 = await loc1Res.Content.ReadFromJsonAsync<StockLocationDto>();

        var loc2Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b2!.Id, "Main Display B2", "TEST_M5_LOC_2", "DISPLAY", "B2 Display Showcase", true, 1));
        var loc2 = await loc2Res.Content.ReadFromJsonAsync<StockLocationDto>();

        // 3. Setup Attributes Masters: Material, Category, Purity, Specifications (PREDEFINED, FREE_TEXT, HYBRID)
        var matRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Test 24K Gold", "TEST_M5_GOLD", "Pure yellow gold", true, 1));
        var mat = await matRes.Content.ReadFromJsonAsync<MaterialDto>();

        var catRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Test Bangles", "TEST_M5_BGL", "Handcrafted sovereign bangles", true, 1));
        var cat = await catRes.Content.ReadFromJsonAsync<CategoryDto>();

        var purRes = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("Test 22K Standard", "TEST_M5_22K", 22.0m, 91.67m, "Standard 22K jewellery", true, 1));
        var pur = await purRes.Content.ReadFromJsonAsync<PurityDto>();

        // Predefined Spec
        var specPredefRes = await ownerClient.PostAsJsonAsync("/api/specifications", new CreateSpecificationRequest("Test Clasp Type", "TEST_M5_CLASP", "Clasp closure classification", true, 1, "PREDEFINED"));
        var specPredef = await specPredefRes.Content.ReadFromJsonAsync<SpecificationDto>();
        var optPredefRes = await ownerClient.PostAsJsonAsync($"/api/specifications/{specPredef!.Id}/options", new CreateSpecificationOptionRequest("Screw Clasp", "TEST_M5_SCREW", "Screw thread lock", true, 1));
        var optPredef = await optPredefRes.Content.ReadFromJsonAsync<SpecificationOptionDto>();

        // Free Text Spec
        var specFreeTextRes = await ownerClient.PostAsJsonAsync("/api/specifications", new CreateSpecificationRequest("Test Hallmark Serial", "TEST_M5_HALLMARK", "Government hallmark laser serial", true, 2, "FREE_TEXT"));
        var specFreeText = await specFreeTextRes.Content.ReadFromJsonAsync<SpecificationDto>();

        // Hybrid Spec
        var specHybridRes = await ownerClient.PostAsJsonAsync("/api/specifications", new CreateSpecificationRequest("Test Design Motif", "TEST_M5_MOTIF", "Artisan motif classification", true, 3, "HYBRID"));
        var specHybrid = await specHybridRes.Content.ReadFromJsonAsync<SpecificationDto>();
        var optHybridRes = await ownerClient.PostAsJsonAsync($"/api/specifications/{specHybrid!.Id}/options", new CreateSpecificationOptionRequest("Peacock Motif", "TEST_M5_PEACOCK", "Traditional peacock pattern", true, 1));
        var optHybrid = await optHybridRes.Content.ReadFromJsonAsync<SpecificationOptionDto>();

        // 4. Setup Cashier user (lacks INVENTORY_CREATE)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cashierRole = await db.Roles.FirstAsync(r => r.Name == SystemRoles.Cashier);

            var userReq = new CreateUserRequest(
                Username: "test_cashier_m5",
                Email: "cashier_m5@saranga.test",
                Password: "CashierPassword@2026!",
                FullName: "Test Cashier M5",
                RoleIds: new List<Guid> { cashierRole.Id },
                BranchIds: new List<Guid> { b1.Id },
                DefaultBranchId: b1.Id
            );
            var cashierUserRes = await ownerClient.PostAsJsonAsync("/api/users", userReq);
            Assert.Equal(HttpStatusCode.Created, cashierUserRes.StatusCode);
        }

        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_cashier_m5", "CashierPassword@2026!"));
        var cashierAuth = await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        var cashierClient = CreateClientWithCookieHandler();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierAuth!.AccessToken);

        // =========================================================================
        // PART 1: Permission Enforcement (Cashier blocked from INVENTORY_CREATE)
        // =========================================================================
        _output.WriteLine("=== PART 1: Permission Enforcement ===");
        var cashierCreateRes = await cashierClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1!.Id,
            Name: "Cashier Attempted Bangle",
            ItemCode: "TEST_M5_CASHIER_FAIL",
            TagNumber: null,
            Barcode: null,
            MaterialId: mat!.Id,
            CategoryId: cat!.Id,
            PurityId: pur!.Id,
            GrossWeight: 15.0m,
            StoneWeight: 1.0m
        ));
        Assert.Equal(HttpStatusCode.Forbidden, cashierCreateRes.StatusCode);
        _output.WriteLine("Verified: Users without INVENTORY_CREATE receive 403 Forbidden.");

        // =========================================================================
        // PART 2: Master Validation & Cross-Branch Isolation
        // =========================================================================
        _output.WriteLine("=== PART 2: Master Validation & Cross-Branch Isolation ===");

        // Cross-branch location assignment rejected
        var crossBranchRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc2!.Id, // Location belongs to Branch 2!
            Name: "Cross Branch Bangle",
            ItemCode: "TEST_M5_CROSS_FAIL",
            TagNumber: null,
            Barcode: null,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 15.0m,
            StoneWeight: 1.0m
        ));
        Assert.Equal(HttpStatusCode.BadRequest, crossBranchRes.StatusCode);

        // Deactivated location rejection
        await ownerClient.PatchAsync($"/api/stock-locations/{loc1.Id}/status", null); // Deactivate loc1
        var inactiveLocRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Inactive Loc Bangle",
            ItemCode: "TEST_M5_INACT_FAIL",
            TagNumber: null,
            Barcode: null,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 15.0m,
            StoneWeight: 1.0m
        ));
        Assert.Equal(HttpStatusCode.BadRequest, inactiveLocRes.StatusCode);
        await ownerClient.PatchAsync($"/api/stock-locations/{loc1.Id}/status", null); // Re-activate loc1
        _output.WriteLine("Verified: Inactive location and cross-branch location rejected.");

        // =========================================================================
        // PART 3: Weight Validation (Strict decimal rules)
        // =========================================================================
        _output.WriteLine("=== PART 3: Precision Weight Rules ===");
        var negativeGrossRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id, StockLocationId: loc1.Id, Name: "Neg Gross", ItemCode: "TEST_M5_NEG_G", TagNumber: null, Barcode: null,
            MaterialId: mat.Id, CategoryId: cat.Id, PurityId: pur.Id, GrossWeight: -10.0m, StoneWeight: 0m));
        Assert.Equal(HttpStatusCode.BadRequest, negativeGrossRes.StatusCode);

        var stoneExceedsGrossRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id, StockLocationId: loc1.Id, Name: "Stone > Gross", ItemCode: "TEST_M5_STONE_ERR", TagNumber: null, Barcode: null,
            MaterialId: mat.Id, CategoryId: cat.Id, PurityId: pur.Id, GrossWeight: 10.0000m, StoneWeight: 12.0000m));
        Assert.Equal(HttpStatusCode.BadRequest, stoneExceedsGrossRes.StatusCode);
        _output.WriteLine("Verified: Negative weights and StoneWeight > GrossWeight rejected.");

        // =========================================================================
        // PART 4: Specification InputType Rules (PREDEFINED, FREE_TEXT, HYBRID)
        // =========================================================================
        _output.WriteLine("=== PART 4: Specification InputType Rules ===");

        // Predefined with custom text -> rejected
        var predefWithCustomRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id, StockLocationId: loc1.Id, Name: "Predef With Custom", ItemCode: "TEST_M5_PREDEF_ERR", TagNumber: null, Barcode: null,
            MaterialId: mat.Id, CategoryId: cat.Id, PurityId: pur.Id, GrossWeight: 10.0m, StoneWeight: 1.0m,
            Specifications: new List<CreateInventoryItemSpecificationRequest> { new(specPredef.Id, optPredef!.Id, "Illegal Text") }));
        Assert.Equal(HttpStatusCode.BadRequest, predefWithCustomRes.StatusCode);

        // FreeText without custom text -> rejected
        var freeTextEmptyRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id, StockLocationId: loc1.Id, Name: "FreeText Empty", ItemCode: "TEST_M5_FT_ERR", TagNumber: null, Barcode: null,
            MaterialId: mat.Id, CategoryId: cat.Id, PurityId: pur.Id, GrossWeight: 10.0m, StoneWeight: 1.0m,
            Specifications: new List<CreateInventoryItemSpecificationRequest> { new(specFreeText!.Id, null, "") }));
        Assert.Equal(HttpStatusCode.BadRequest, freeTextEmptyRes.StatusCode);

        // FreeText selecting predefined option -> rejected
        var freeTextWithOptionRes = await ownerClient.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest(
            BranchId: b1.Id, StockLocationId: loc1.Id, Name: "FreeText With Option", ItemCode: "TEST_M5_FT_OPT_ERR", TagNumber: null, Barcode: null,
            MaterialId: mat.Id, CategoryId: cat.Id, PurityId: pur.Id, GrossWeight: 10.0m, StoneWeight: 1.0m,
            Specifications: new List<CreateInventoryItemSpecificationRequest> { new(specFreeText.Id, optPredef.Id, "Some Text") }));
        Assert.Equal(HttpStatusCode.BadRequest, freeTextWithOptionRes.StatusCode);
        _output.WriteLine("Verified: Specification InputType rules (PREDEFINED and FREE_TEXT) strictly enforced.");

        // =========================================================================
        // PART 5: Media Upload & Validation
        // =========================================================================
        _output.WriteLine("=== PART 5: Media Upload & File Validation ===");
        var imageBytes = Encoding.UTF8.GetBytes("fake-jpeg-image-binary-stream-for-testing");
        using var imageContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        imageContent.Add(fileContent, "file", "bangle_front_view.jpg");

        var uploadRes = await ownerClient.PostAsync("/api/inventory/upload-file", imageContent);
        Assert.Equal(HttpStatusCode.OK, uploadRes.StatusCode);
        var uploadResult = await uploadRes.Content.ReadFromJsonAsync<FileUploadResultDto>();
        Assert.NotNull(uploadResult);
        Assert.Equal("bangle_front_view.jpg", uploadResult.FileName);
        Assert.Equal(1, uploadResult.MediaType); // Image

        // Unsupported media type rejection
        using var invalidContent = new MultipartFormDataContent();
        var exeContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake-exe-binary"));
        exeContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        invalidContent.Add(exeContent, "file", "malicious_payload.exe");
        var invalidUploadRes = await ownerClient.PostAsync("/api/inventory/upload-file", invalidContent);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUploadRes.StatusCode);
        _output.WriteLine("Verified: Image upload succeeded and unsupported executable rejected.");

        // =========================================================================
        // PART 6: Full Manual Item Creation (All 8 Steps data combined)
        // =========================================================================
        _output.WriteLine("=== PART 6: Complete Manual Item Creation ===");
        var createReq = new CreateInventoryItemRequest(
            BranchId: b1.Id,
            StockLocationId: loc1.Id,
            Name: "Test M5 Sovereign Handcrafted Bangle",
            ItemCode: "TEST_M5_SOV_BGL_001",
            TagNumber: null, // Auto-generated
            Barcode: null,   // Auto-generated
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 15.8500m,
            StoneWeight: 1.3500m,
            PieceCount: 1,
            MakingChargeMethod: 1, // PER_GRAM
            MakingChargeValue: 2500.00m,
            Description: "Artisan Sovereign gold bangle with screw clasp and hallmark engraving",
            IsActive: true,
            Specifications: new List<CreateInventoryItemSpecificationRequest>
            {
                new(specPredef.Id, optPredef.Id, null),
                new(specFreeText.Id, null, "HM-SL-2026-99881"),
                new(specHybrid!.Id, optHybrid!.Id, "Custom hand-carved finish")
            },
            Media: new List<CreateInventoryItemMediaRequest>
            {
                new(uploadResult.FileName, uploadResult.StoragePath, uploadResult.ContentType, uploadResult.FileSize, uploadResult.MediaType, 1, true)
            }
        );

        var createRes = await ownerClient.PostAsJsonAsync("/api/inventory", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdItem = await createRes.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.NotNull(createdItem);

        // Verifications
        Assert.NotEmpty(createdItem.TagNumber);
        Assert.NotEmpty(createdItem.Barcode);
        Assert.StartsWith(b1.Code, createdItem.TagNumber);
        Assert.Equal(14.5000m, createdItem.NetWeight); // 15.8500 - 1.3500
        Assert.Equal(1, createdItem.PieceCount);
        Assert.Equal(1, createdItem.Status); // Available
        Assert.True(createdItem.IsActive);
        Assert.Equal(3, createdItem.Specifications.Count);
        Assert.Single(createdItem.Media);
        Assert.True(createdItem.Media[0].IsPrimary);
        Assert.True(createdItem.Version > 0);
        _output.WriteLine($"Verified: Manual item created with Tag='{createdItem.TagNumber}', Barcode='{createdItem.Barcode}', NetWeight={createdItem.NetWeight}g.");

        // =========================================================================
        // PART 7: Item Editing, Tag Immutability & Concurrency Conflict (409)
        // =========================================================================
        _output.WriteLine("=== PART 7: Edit Concurrency & Immutability ===");

        // User A reads item with initial Version
        var initialVersion = createdItem.Version;

        // User B edits and saves item
        var updateReqB = new UpdateInventoryItemRequest(
            StockLocationId: loc1.Id,
            Name: "Test M5 Sovereign Handcrafted Bangle - User B Edit",
            ItemCode: createdItem.ItemCode,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 16.0000m,
            StoneWeight: 1.0000m,
            PieceCount: 1,
            MakingChargeMethod: 2, // PER_ITEM
            MakingChargeValue: 35000.00m,
            Description: "Updated by User B",
            IsActive: true,
            Version: initialVersion
        );

        var updateResB = await ownerClient.PutAsJsonAsync($"/api/inventory/{createdItem.Id}", updateReqB);
        Assert.Equal(HttpStatusCode.OK, updateResB.StatusCode);
        var updatedItemB = await updateResB.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.NotNull(updatedItemB);
        Assert.NotEqual(initialVersion, updatedItemB.Version);

        // Tag and Barcode must remain identical
        Assert.Equal(createdItem.TagNumber, updatedItemB.TagNumber);
        Assert.Equal(createdItem.Barcode, updatedItemB.Barcode);
        Assert.Equal(15.0000m, updatedItemB.NetWeight);

        // User A attempts to save with stale Version (initialVersion)
        var staleUpdateReqA = new UpdateInventoryItemRequest(
            StockLocationId: loc1.Id,
            Name: "Test M5 Sovereign Handcrafted Bangle - User A Stale Overwrite",
            ItemCode: createdItem.ItemCode,
            MaterialId: mat.Id,
            CategoryId: cat.Id,
            PurityId: pur.Id,
            GrossWeight: 16.5000m,
            StoneWeight: 1.5000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 3000.00m,
            Description: "User A trying to overwrite",
            IsActive: true,
            Version: initialVersion // Stale concurrency token!
        );

        var conflictRes = await ownerClient.PutAsJsonAsync($"/api/inventory/{createdItem.Id}", staleUpdateReqA);
        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);
        var conflictBody = await conflictRes.Content.ReadAsStringAsync();
        Assert.Contains("modified by another user", conflictBody);
        _output.WriteLine("Verified: Optimistic concurrency conflict returns 409 Conflict with clear refresh instruction.");

        // =========================================================================
        // PART 8: SOLD Status Immutability
        // =========================================================================
        _output.WriteLine("=== PART 8: SOLD Status Immutability ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(@"UPDATE ""InventoryItems"" SET ""Status"" = 3 WHERE ""Id"" = {0}", createdItem.Id);
        }

        // Attempting to edit SOLD item is rejected
        var soldEditRes = await ownerClient.PutAsJsonAsync($"/api/inventory/{createdItem.Id}", updateReqB);
        Assert.Equal(HttpStatusCode.BadRequest, soldEditRes.StatusCode);

        // Attempting to toggle active status on SOLD item is rejected
        var soldToggleRes = await ownerClient.PatchAsync($"/api/inventory/{createdItem.Id}/toggle-active", null);
        Assert.Equal(HttpStatusCode.BadRequest, soldToggleRes.StatusCode);
        _output.WriteLine("Verified: SOLD items are read-only and locked against modification.");

        // =========================================================================
        // PART 9: Audit Log Verification
        // =========================================================================
        _output.WriteLine("=== PART 9: Audit Logging ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audits = await db.AuditLogs
                .Where(a => a.EntityName == "InventoryItem" && a.EntityId == createdItem.Id.ToString())
                .ToListAsync();

            Assert.True(audits.Count >= 2);
            Assert.Contains(audits, a => a.Action == "CREATE");
            Assert.Contains(audits, a => a.Action == "UPDATE");
            _output.WriteLine($"Verified: {audits.Count} audit records created with initial parameters and update diffs.");
        }
    }

    [Fact]
    public async Task ManualItemCreation_IdempotencyAndMediaLifecycle_HardenedVerification()
    {
        var client = CreateClientWithCookieHandler();

        // 1. Authenticate as Owner / Super Admin
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var ownerToken = authData!.AccessToken;

        var ownerClient = CreateClientWithCookieHandler();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // 2. Setup branch, location, and master references
        var b1Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M5 Branch 1", "TEST-M5-B1", "LK", "LKR", "Asia/Colombo", true));
        var b1 = await b1Res.Content.ReadFromJsonAsync<BranchDto>();

        var loc1Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1!.Id, "Main Display B1", "TEST_M5_LOC_1", "DISPLAY", "B1 Display Showcase", true, 1));
        var loc1 = await loc1Res.Content.ReadFromJsonAsync<StockLocationDto>();

        var matRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Test M5 Gold", "TEST_M5_GOLD", "Test Material", true, 1));
        var mat = await matRes.Content.ReadFromJsonAsync<MaterialDto>();

        var catRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Test M5 Bangles", "TEST_M5_BAN", "Test Category", true, 1));
        var cat = await catRes.Content.ReadFromJsonAsync<CategoryDto>();

        var purRes = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("Test M5 22K", "TEST_M5_22K", 22.0m, 91.67m, "Test Purity", true, 1));
        var pur = await purRes.Content.ReadFromJsonAsync<PurityDto>();

        // 3. Test Temporary Media Upload
        _output.WriteLine("=== Testing Temporary Media Upload ===");
        var imageBytes = Encoding.UTF8.GetBytes("FAKE_TEST_JPEG_IMAGE_CONTENT_M5_HARDENING");
        using var multiContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multiContent.Add(fileContent, "file", "test_item_sample.jpg");

        var uploadRes = await ownerClient.PostAsync("/api/inventory/upload-file", multiContent);
        Assert.Equal(HttpStatusCode.OK, uploadRes.StatusCode);
        var uploadedFile = await uploadRes.Content.ReadFromJsonAsync<FileUploadResultDto>();
        Assert.NotNull(uploadedFile);
        Assert.StartsWith("/uploads/temp/", uploadedFile.StoragePath);
        _output.WriteLine($"Verified: Uploaded file is staged at temporary location '{uploadedFile.StoragePath}'.");

        // 4. Test Idempotency: Create Item with Idempotency-Key
        _output.WriteLine("=== Testing Idempotent Item Creation ===");
        var idempotencyKey = "test-m5-idemp-001";
        var createReq = new CreateInventoryItemRequest(
            BranchId: b1!.Id,
            StockLocationId: loc1!.Id,
            Name: "Test M5 Idempotent Bangle",
            ItemCode: "TEST_M5_IDEMP_001",
            TagNumber: "TEST-M5-IDEMP-001",
            Barcode: "TEST-M5-IDEMP-BAR-001",
            MaterialId: mat.Id,
            CategoryId: cat!.Id,
            PurityId: pur!.Id,
            GrossWeight: 18.5000m,
            StoneWeight: 0.5000m,
            PieceCount: 1,
            MakingChargeMethod: 1,
            MakingChargeValue: 3500.00m,
            Description: "Idempotency test item",
            IsActive: true,
            Specifications: null,
            Media: new List<CreateInventoryItemMediaRequest>
            {
                new(uploadedFile.FileName, uploadedFile.StoragePath, uploadedFile.ContentType, uploadedFile.FileSize, uploadedFile.MediaType, 0, true)
            }
        );

        var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inventory")
        {
            Content = JsonContent.Create(createReq)
        };
        firstRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var firstRes = await ownerClient.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstRes.StatusCode);
        var firstItem = await firstRes.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.NotNull(firstItem);
        Assert.Equal("TEST-M5-IDEMP-001", firstItem.TagNumber);

        // Verify media was promoted from temp to inventory permanent directory
        Assert.Single(firstItem.Media);
        Assert.StartsWith("/uploads/inventory/", firstItem.Media[0].StoragePath);
        _output.WriteLine($"Verified: Temporary media successfully finalized to '{firstItem.Media[0].StoragePath}'.");

        // Send IDENTICAL request with the SAME Idempotency-Key
        var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inventory")
        {
            Content = JsonContent.Create(createReq)
        };
        secondRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var secondRes = await ownerClient.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.Created, secondRes.StatusCode);
        var secondItem = await secondRes.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.NotNull(secondItem);
        Assert.Equal(firstItem.Id, secondItem.Id);
        Assert.Equal(firstItem.TagNumber, secondItem.TagNumber);
        Assert.Equal(firstItem.Barcode, secondItem.Barcode);
        _output.WriteLine("Verified: Replayed create request returned the exact same item without duplicating.");

        // Check DB directly: exactly one item and one idempotency record exist
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var itemCount = await db.InventoryItems.CountAsync(i => i.TagNumber == "TEST-M5-IDEMP-001");
            Assert.Equal(1, itemCount);

            var idempCount = await db.IdempotencyRecords.CountAsync(r => r.Key == idempotencyKey);
            Assert.Equal(1, idempCount);
            _output.WriteLine("Verified: Database contains exactly 1 InventoryItem and 1 IdempotencyRecord.");
        }

        // Send DIFFERENT payload with the SAME Idempotency-Key -> Expected 400 Bad Request
        var modifiedReq = createReq with { GrossWeight = 99.9999m, Name = "Tampered Payload Name" };
        var tamperedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inventory")
        {
            Content = JsonContent.Create(modifiedReq)
        };
        tamperedRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var tamperedRes = await ownerClient.SendAsync(tamperedRequest);
        Assert.Equal(HttpStatusCode.BadRequest, tamperedRes.StatusCode);
        var tamperedError = await tamperedRes.Content.ReadAsStringAsync();
        Assert.Contains("Idempotency key has already been used with a different request payload", tamperedError);
        _output.WriteLine("Verified: Reusing idempotency key with different payload was rejected with 400 Bad Request.");

        // 5. Test INVENTORY_EDIT Media Authorization & Lifecycle
        _output.WriteLine("=== Testing INVENTORY_EDIT Media Upload & Attachment ===");
        Guid editorRoleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var viewPerm = await db.Permissions.FirstAsync(p => p.Name == Permissions.InventoryView);
            var editPerm = await db.Permissions.FirstAsync(p => p.Name == Permissions.InventoryEdit);

            var editorRoleReq = new CreateRoleRequest("Test M5 Inventory Editor", "Role with edit permission only", new List<Guid> { viewPerm.Id, editPerm.Id });
            var roleRes = await ownerClient.PostAsJsonAsync("/api/roles", editorRoleReq);
            Assert.Equal(HttpStatusCode.Created, roleRes.StatusCode);
            var editorRole = await roleRes.Content.ReadFromJsonAsync<RoleDto>();
            editorRoleId = editorRole!.Id;

            var userReq = new CreateUserRequest(
                Username: "test_editor_m5",
                Email: "editor_m5@saranga.test",
                Password: "EditorPassword@2026!",
                FullName: "Test Editor M5",
                RoleIds: new List<Guid> { editorRoleId },
                BranchIds: new List<Guid> { b1.Id },
                DefaultBranchId: b1.Id
            );
            var userRes = await ownerClient.PostAsJsonAsync("/api/users", userReq);
            Assert.Equal(HttpStatusCode.Created, userRes.StatusCode);
        }

        // Login as editor
        var editorLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_editor_m5", "EditorPassword@2026!"));
        Assert.Equal(HttpStatusCode.OK, editorLoginRes.StatusCode);
        var editorAuth = await editorLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        var editorClient = CreateClientWithCookieHandler();
        editorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", editorAuth!.AccessToken);

        // Editor attempts to create an item -> 403 Forbidden (no INVENTORY_CREATE)
        var forbiddenCreate = await editorClient.PostAsJsonAsync("/api/inventory", createReq);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
        _output.WriteLine("Verified: User with only INVENTORY_EDIT is forbidden from creating new items.");

        // Editor attaches media to existing item via POST /api/inventory/{id}/media -> 200 OK
        using var attachMulti = new MultipartFormDataContent();
        var attachBytes = Encoding.UTF8.GetBytes("ATTACHED_IMAGE_M5_BY_EDITOR");
        var attachFileContent = new ByteArrayContent(attachBytes);
        attachFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        attachMulti.Add(attachFileContent, "file", "attached_by_editor.jpg");

        var attachRes = await editorClient.PostAsync($"/api/inventory/{firstItem.Id}/media?isPrimary=false", attachMulti);
        Assert.Equal(HttpStatusCode.OK, attachRes.StatusCode);
        var attachedMedia = await attachRes.Content.ReadFromJsonAsync<InventoryItemMediaDto>();
        Assert.NotNull(attachedMedia);
        Assert.Equal("attached_by_editor.jpg", attachedMedia.FileName);
        _output.WriteLine($"Verified: User with INVENTORY_EDIT successfully attached media '{attachedMedia.FileName}'.");

        // Editor removes attached media via DELETE /api/inventory/{id}/media/{mediaId} -> 204 NoContent
        var removeRes = await editorClient.DeleteAsync($"/api/inventory/{firstItem.Id}/media/{attachedMedia.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeRes.StatusCode);
        _output.WriteLine("Verified: User with INVENTORY_EDIT successfully removed attached media.");

        // 6. Test Cleanup of Expired Temp Media
        _output.WriteLine("=== Testing Cleanup of Expired Temp Uploads ===");
        var cleanupRes = await ownerClient.PostAsync("/api/inventory/cleanup-temp-media?maxAgeHours=0", null);
        Assert.Equal(HttpStatusCode.OK, cleanupRes.StatusCode);
        _output.WriteLine("Verified: Temp media cleanup endpoint succeeded.");
    }
}

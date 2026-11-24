using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Api;
using Saranga.Application.DTOs.Attributes;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.DTOs.Branches;
using Saranga.Application.DTOs.Inventory;
using Saranga.Application.DTOs.Roles;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module7IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public Module7IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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
            DELETE FROM ""InventoryImportBatches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T7B1', 'T7B2'));
            DELETE FROM ""IdempotencyRecords"" WHERE ""Key"" LIKE 'test-m7-%';
            DELETE FROM ""InventoryItemSpecifications"" WHERE ""InventoryItemId"" IN (
                SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M7_%' OR ""TagNumber"" LIKE 'T7B1-%' OR ""Name"" LIKE 'Test M7%'
            );
            DELETE FROM ""InventoryItemMedia"" WHERE ""InventoryItemId"" IN (
                SELECT ""Id"" FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M7_%' OR ""TagNumber"" LIKE 'T7B1-%' OR ""Name"" LIKE 'Test M7%'
            );
            DELETE FROM ""InventoryItems"" WHERE ""ItemCode"" LIKE 'TEST_M7_%' OR ""TagNumber"" LIKE 'T7B1-%' OR ""Name"" LIKE 'Test M7%';
            DELETE FROM ""StockLocations"" WHERE ""Code"" LIKE 'TEST_M7_%';
            DELETE FROM ""Purities"" WHERE ""Code"" LIKE 'TEST_M7_%';
            DELETE FROM ""Categories"" WHERE ""Code"" LIKE 'TEST_M7_%';
            DELETE FROM ""Materials"" WHERE ""Code"" LIKE 'TEST_M7_%';
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T7B1', 'T7B2'));
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m7', 'test_manager_m7'));
            DELETE FROM ""Users"" WHERE ""Username"" IN ('test_cashier_m7', 'test_manager_m7');
            DELETE FROM ""RolePermissions"" WHERE ""RoleId"" IN (SELECT ""Id"" FROM ""Roles"" WHERE ""Name"" LIKE 'Test M7%');
            DELETE FROM ""Roles"" WHERE ""Name"" LIKE 'Test M7%';
            DELETE FROM ""Branches"" WHERE ""Code"" IN ('T7B1', 'T7B2');
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
    public async Task ComprehensiveModule7_BulkImport_EndToEndVerification()
    {
        var client = CreateClientWithCookieHandler();

        // =========================================================================
        // 1. Authenticate as Owner / Super Admin
        // =========================================================================
        _output.WriteLine("=== 1. Authenticate as Owner / Super Admin ===");
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var authData = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authData?.AccessToken);

        var ownerClient = CreateClientWithCookieHandler();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.AccessToken);

        // =========================================================================
        // 2. Setup Test Branches and Stock Locations
        // =========================================================================
        _output.WriteLine("=== 2. Setup Branches & Stock Locations ===");
        var b1Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M7 Colombo Main", "T7B1", "LK", "LKR", "Asia/Colombo"));
        Assert.Equal(HttpStatusCode.Created, b1Res.StatusCode);
        var b1 = await b1Res.Content.ReadFromJsonAsync<BranchDto>();

        var b2Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M7 Jaffna Branch", "T7B2", "LK", "LKR", "Asia/Colombo"));
        Assert.Equal(HttpStatusCode.Created, b2Res.StatusCode);
        var b2 = await b2Res.Content.ReadFromJsonAsync<BranchDto>();

        // Stock Locations: loc1 belongs to T7B1, loc2 belongs to T7B2
        var loc1Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b1!.Id, "Test M7 Vault 1", "TEST_M7_LOC1", "VAULT", "Safe 1"));
        Assert.Equal(HttpStatusCode.Created, loc1Res.StatusCode);
        var loc1 = await loc1Res.Content.ReadFromJsonAsync<StockLocationDto>();

        var loc2Res = await ownerClient.PostAsJsonAsync("/api/stock-locations", new CreateStockLocationRequest(b2!.Id, "Test M7 Display 2", "TEST_M7_LOC2", "DISPLAY", "Counter"));
        Assert.Equal(HttpStatusCode.Created, loc2Res.StatusCode);
        var loc2 = await loc2Res.Content.ReadFromJsonAsync<StockLocationDto>();

        // =========================================================================
        // 3. Setup Master Data (Materials, Categories, Purities)
        // =========================================================================
        _output.WriteLine("=== 3. Setup Master Data ===");
        var matGoldRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Test M7 Gold", "TEST_M7_GOLD", "Gold material"));
        Assert.Equal(HttpStatusCode.Created, matGoldRes.StatusCode);

        var matInactRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Test M7 Inactive Metal", "TEST_M7_INACT", "Inactive"));
        Assert.Equal(HttpStatusCode.Created, matInactRes.StatusCode);
        var matInact = await matInactRes.Content.ReadFromJsonAsync<MaterialDto>();
        // Toggle to inactive via /api/materials/{id}/status
        var toggleRes = await ownerClient.PatchAsync($"/api/materials/{matInact!.Id}/status", null);
        Assert.Equal(HttpStatusCode.OK, toggleRes.StatusCode);

        var catBglRes = await ownerClient.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Test M7 Bangles", "TEST_M7_BGL", "Bangles"));
        Assert.Equal(HttpStatusCode.Created, catBglRes.StatusCode);

        var pur22Res = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("Test M7 22K", "TEST_M7_22K", 22.0m, 91.67m, "Standard 22K", true, 1));
        Assert.Equal(HttpStatusCode.Created, pur22Res.StatusCode);

        // =========================================================================
        // 4. Download Template Verification (.xlsx and .csv)
        // =========================================================================
        _output.WriteLine("=== 4. Download Template Verification ===");
        var excelTplRes = await ownerClient.GetAsync("/api/inventory/import/template?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, excelTplRes.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelTplRes.Content.Headers.ContentType?.MediaType);
        var excelBytes = await excelTplRes.Content.ReadAsByteArrayAsync();
        Assert.True(excelBytes.Length > 0);

        var csvTplRes = await ownerClient.GetAsync("/api/inventory/import/template?format=csv");
        Assert.Equal(HttpStatusCode.OK, csvTplRes.StatusCode);
        Assert.Equal("text/csv", csvTplRes.Content.Headers.ContentType?.MediaType);
        var csvContent = await csvTplRes.Content.ReadAsStringAsync();
        Assert.Contains("Item Name", csvContent);
        Assert.Contains("Material Code", csvContent);
        Assert.Contains("Stock Location Code", csvContent);

        // =========================================================================
        // 5. Invalid File Type & Malformed Input Handling
        // =========================================================================
        _output.WriteLine("=== 5. Invalid File Type & Empty File Handling ===");
        // Reject .txt file
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("plain text file"));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Add(fileContent, "file", "test.txt");

            var invalidExtRes = await ownerClient.PostAsync($"/api/inventory/import/validate?branchId={b1.Id}", content);
            Assert.Equal(HttpStatusCode.BadRequest, invalidExtRes.StatusCode);
            var err = await invalidExtRes.Content.ReadAsStringAsync();
            Assert.Contains("Unsupported file type", err);
        }

        // Reject empty file
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(Array.Empty<byte>());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", "empty.csv");

            var emptyRes = await ownerClient.PostAsync($"/api/inventory/import/validate?branchId={b1.Id}", content);
            Assert.Equal(HttpStatusCode.BadRequest, emptyRes.StatusCode);
        }

        // =========================================================================
        // 6. Missing Required Column Headers
        // =========================================================================
        _output.WriteLine("=== 6. Missing Required Columns Handling ===");
        var badHeadersCsv = "Item Name,Description\nRing,Nice Ring";
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(badHeadersCsv));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", "missing_cols.csv");

            var badHeadersRes = await ownerClient.PostAsync($"/api/inventory/import/validate?branchId={b1.Id}", content);
            Assert.Equal(HttpStatusCode.BadRequest, badHeadersRes.StatusCode);
            var err = await badHeadersRes.Content.ReadAsStringAsync();
            Assert.Contains("Missing required column(s)", err);
        }

        // =========================================================================
        // 7. Comprehensive Row-Level Validation & Error Reporting
        // =========================================================================
        _output.WriteLine("=== 7. Row-Level Domain Validation & Error Reporting ===");
        var invalidRowsCsv = new StringBuilder();
        invalidRowsCsv.AppendLine("Item Name,Item Code,Material Code,Category Code,Purity Code,Gross Weight,Stone Weight,Piece Count,Stock Location Code,Making Charge Method,Making Charge Value,Description");
        // Row 2: Unknown Material
        invalidRowsCsv.AppendLine("Item Unknown Mat,TEST_M7_01,UNKNOWN_MAT,TEST_M7_BGL,TEST_M7_22K,10.0,0.0,1,TEST_M7_LOC1,PER_GRAM,1000,Desc");
        // Row 3: Inactive Material
        invalidRowsCsv.AppendLine("Item Inact Mat,TEST_M7_02,TEST_M7_INACT,TEST_M7_BGL,TEST_M7_22K,10.0,0.0,1,TEST_M7_LOC1,PER_GRAM,1000,Desc");
        // Row 4: Cross-Branch Location (TEST_M7_LOC2 is in T7B2, but import is for T7B1)
        invalidRowsCsv.AppendLine("Item Cross Branch Loc,TEST_M7_03,TEST_M7_GOLD,TEST_M7_BGL,TEST_M7_22K,10.0,0.0,1,TEST_M7_LOC2,PER_GRAM,1000,Desc");
        // Row 5: Gross Weight <= 0
        invalidRowsCsv.AppendLine("Item Zero Gross,TEST_M7_04,TEST_M7_GOLD,TEST_M7_BGL,TEST_M7_22K,-5.0,0.0,1,TEST_M7_LOC1,PER_GRAM,1000,Desc");
        // Row 6: Stone Weight > Gross Weight
        invalidRowsCsv.AppendLine("Item Heavy Stone,TEST_M7_05,TEST_M7_GOLD,TEST_M7_BGL,TEST_M7_22K,10.0,15.0,1,TEST_M7_LOC1,PER_GRAM,1000,Desc");
        // Row 7: Invalid Making Charge Method
        invalidRowsCsv.AppendLine("Item Bad Charge,TEST_M7_06,TEST_M7_GOLD,TEST_M7_BGL,TEST_M7_22K,10.0,0.0,1,TEST_M7_LOC1,INVALID_CHARGE,1000,Desc");

        BulkImportValidationResultDto? invalidBatchResult;
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(invalidRowsCsv.ToString()));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", "invalid_rows.csv");

            var invalidRes = await ownerClient.PostAsync($"/api/inventory/import/validate?branchId={b1.Id}", content);
            Assert.Equal(HttpStatusCode.OK, invalidRes.StatusCode);
            invalidBatchResult = await invalidRes.Content.ReadFromJsonAsync<BulkImportValidationResultDto>();
        }

        Assert.NotNull(invalidBatchResult);
        Assert.False(invalidBatchResult.IsAllValid);
        Assert.Equal(6, invalidBatchResult.TotalRows);
        Assert.Equal(0, invalidBatchResult.ValidRows);
        Assert.Equal(6, invalidBatchResult.InvalidRows);

        // Verify specific field-level error messages
        Assert.Contains(invalidBatchResult.Errors, e => e.Field == "MaterialCode" && e.Message.Contains("UNKNOWN_MAT"));
        Assert.Contains(invalidBatchResult.Errors, e => e.Field == "MaterialCode" && e.Message.Contains("inactive"));
        Assert.Contains(invalidBatchResult.Errors, e => e.Field == "StockLocationCode" && e.Message.Contains("does not belong to selected branch"));
        Assert.Contains(invalidBatchResult.Errors, e => e.Field == "GrossWeight" && e.Message.Contains("greater than zero"));
        Assert.Contains(invalidBatchResult.Errors, e => e.Field == "StoneWeight" && e.Message.Contains("cannot exceed Gross Weight"));
        Assert.Contains(invalidBatchResult.Errors, e => e.Field == "MakingChargeMethod" && e.Message.Contains("Invalid making charge method"));

        // =========================================================================
        // 8. All-or-Nothing Atomicity Enforcement
        // =========================================================================
        _output.WriteLine("=== 8. All-or-Nothing Atomicity Enforcement ===");
        // Attempting to confirm the invalid batch MUST be rejected
        var failConfirmRes = await ownerClient.PostAsJsonAsync("/api/inventory/import/confirm", new ConfirmBulkImportRequest(invalidBatchResult.BatchId));
        Assert.Equal(HttpStatusCode.BadRequest, failConfirmRes.StatusCode);
        var failConfirmBody = await failConfirmRes.Content.ReadAsStringAsync();
        Assert.Contains("All rows must be valid before confirming import", failConfirmBody);

        // Verify ZERO inventory items were created
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var count = await db.InventoryItems.CountAsync(i => i.BranchId == b1.Id);
            Assert.Equal(0, count);
        }
        _output.WriteLine("Verified: Exactly 0 items created in database when batch contains errors.");

        // =========================================================================
        // 9. Valid CSV Import & Atomic Creation
        // =========================================================================
        _output.WriteLine("=== 9. Valid CSV Import & Atomic Creation ===");
        var validCsv = new StringBuilder();
        validCsv.AppendLine("Item Name,Item Code,Material Code,Category Code,Purity Code,Gross Weight,Stone Weight,Piece Count,Stock Location Code,Making Charge Method,Making Charge Value,Description");
        validCsv.AppendLine("Test M7 Gold Bangle 22K,TEST_M7_CSV1,TEST_M7_GOLD,TEST_M7_BGL,TEST_M7_22K,16.5000,0.5000,1,TEST_M7_LOC1,PER_GRAM,2500.00,Handcrafted traditional bangle");
        validCsv.AppendLine("Test M7 Gold Ring 22K,TEST_M7_CSV2,TEST_M7_GOLD,TEST_M7_BGL,TEST_M7_22K,8.0000,0.0000,1,TEST_M7_LOC1,PER_ITEM,4000.00,Plain gold band");

        BulkImportValidationResultDto? validCsvValidation;
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(validCsv.ToString()));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", "valid_stock.csv");

            var validRes = await ownerClient.PostAsync($"/api/inventory/import/validate?branchId={b1.Id}", content);
            Assert.Equal(HttpStatusCode.OK, validRes.StatusCode);
            validCsvValidation = await validRes.Content.ReadFromJsonAsync<BulkImportValidationResultDto>();
        }

        Assert.NotNull(validCsvValidation);
        Assert.True(validCsvValidation.IsAllValid);
        Assert.Equal(2, validCsvValidation.TotalRows);
        Assert.Equal(2, validCsvValidation.ValidRows);
        Assert.Equal(0, validCsvValidation.InvalidRows);

        // Verify net weight calculation on preview
        var row1 = validCsvValidation.Rows.First(r => r.ItemCode == "TEST_M7_CSV1");
        Assert.Equal(16.5000m, row1.GrossWeight);
        Assert.Equal(0.5000m, row1.StoneWeight);
        Assert.Equal(16.0000m, row1.NetWeight);

        // Confirm Valid Batch with Idempotency Key
        string idempotencyKey = "test-m7-idemp-" + Guid.NewGuid();
        var confirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/import/confirm")
        {
            Content = JsonContent.Create(new ConfirmBulkImportRequest(validCsvValidation.BatchId))
        };
        confirmReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var confirmRes = await ownerClient.SendAsync(confirmReq);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        var importSummary = await confirmRes.Content.ReadFromJsonAsync<BulkImportResultDto>();

        Assert.NotNull(importSummary);
        Assert.Equal(2, importSummary.TotalImported);
        Assert.Equal("COMPLETED", importSummary.Status);
        Assert.Equal(2, importSummary.CreatedItems.Count);

        // Verify generated TagNumber and Barcode format
        foreach (var item in importSummary.CreatedItems)
        {
            Assert.StartsWith("T7B1-TEST_M7_BGL-", item.TagNumber);
            Assert.StartsWith("SRGT7B1", item.Barcode);
        }

        // =========================================================================
        // 10. Idempotency Verification
        // =========================================================================
        _output.WriteLine("=== 10. Idempotency Verification ===");
        // Re-submitting the exact same confirmation with the same Idempotency-Key
        var dupConfirmReq = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/import/confirm")
        {
            Content = JsonContent.Create(new ConfirmBulkImportRequest(validCsvValidation.BatchId))
        };
        dupConfirmReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var dupConfirmRes = await ownerClient.SendAsync(dupConfirmReq);
        Assert.Equal(HttpStatusCode.OK, dupConfirmRes.StatusCode);
        var dupImportSummary = await dupConfirmRes.Content.ReadFromJsonAsync<BulkImportResultDto>();

        Assert.NotNull(dupImportSummary);
        Assert.Equal(validCsvValidation.BatchId, dupImportSummary.BatchId);
        Assert.Equal(2, dupImportSummary.TotalImported);

        // Verify that database STILL contains exactly 2 items, NOT 4
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var count = await db.InventoryItems.CountAsync(i => i.BranchId == b1.Id);
            Assert.Equal(2, count);
        }
        _output.WriteLine("Verified: Duplicate confirmation with same idempotency key returned cached result without duplicating items.");

        // Reusing idempotency key for a different batch MUST be rejected
        var bogusBatchId = Guid.NewGuid();
        var alteredReq = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/import/confirm")
        {
            Content = JsonContent.Create(new ConfirmBulkImportRequest(bogusBatchId))
        };
        alteredReq.Headers.Add("Idempotency-Key", idempotencyKey);

        var alteredRes = await ownerClient.SendAsync(alteredReq);
        // Returns 404 (batch not found) or 400 (different payload with same key)
        Assert.True(alteredRes.StatusCode == HttpStatusCode.NotFound || alteredRes.StatusCode == HttpStatusCode.BadRequest);

        // =========================================================================
        // 11. Valid Excel (.xlsx) Import Verification
        // =========================================================================
        _output.WriteLine("=== 11. Valid Excel (.xlsx) Import Verification ===");
        byte[] validXlsxBytes;
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Inventory Items");
            ws.Cell(1, 1).Value = "Item Name";
            ws.Cell(1, 2).Value = "Item Code";
            ws.Cell(1, 3).Value = "Material Code";
            ws.Cell(1, 4).Value = "Category Code";
            ws.Cell(1, 5).Value = "Purity Code";
            ws.Cell(1, 6).Value = "Gross Weight";
            ws.Cell(1, 7).Value = "Stone Weight";
            ws.Cell(1, 8).Value = "Piece Count";
            ws.Cell(1, 9).Value = "Stock Location Code";
            ws.Cell(1, 10).Value = "Making Charge Method";
            ws.Cell(1, 11).Value = "Making Charge Value";
            ws.Cell(1, 12).Value = "Description";

            // Row 2
            ws.Cell(2, 1).Value = "Test M7 Excel Bangle";
            ws.Cell(2, 2).Value = "TEST_M7_XLS1";
            ws.Cell(2, 3).Value = "TEST_M7_GOLD";
            ws.Cell(2, 4).Value = "TEST_M7_BGL";
            ws.Cell(2, 5).Value = "TEST_M7_22K";
            ws.Cell(2, 6).Value = 20.0000;
            ws.Cell(2, 7).Value = 1.0000;
            ws.Cell(2, 8).Value = 1;
            ws.Cell(2, 9).Value = "TEST_M7_LOC1";
            ws.Cell(2, 10).Value = "PER_GRAM";
            ws.Cell(2, 11).Value = 3000.00;
            ws.Cell(2, 12).Value = "Created from Excel XLSX";

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            validXlsxBytes = ms.ToArray();
        }

        BulkImportValidationResultDto? xlsxValidation;
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(validXlsxBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            content.Add(fileContent, "file", "valid_stock.xlsx");

            var xlsxRes = await ownerClient.PostAsync($"/api/inventory/import/validate?branchId={b1.Id}", content);
            Assert.Equal(HttpStatusCode.OK, xlsxRes.StatusCode);
            xlsxValidation = await xlsxRes.Content.ReadFromJsonAsync<BulkImportValidationResultDto>();
        }

        Assert.NotNull(xlsxValidation);
        Assert.True(xlsxValidation.IsAllValid);
        Assert.Equal(1, xlsxValidation.TotalRows);
        Assert.Equal(1, xlsxValidation.ValidRows);

        var confirmXlsxRes = await ownerClient.PostAsJsonAsync("/api/inventory/import/confirm", new ConfirmBulkImportRequest(xlsxValidation.BatchId));
        Assert.Equal(HttpStatusCode.OK, confirmXlsxRes.StatusCode);
        var xlsxSummary = await confirmXlsxRes.Content.ReadFromJsonAsync<BulkImportResultDto>();
        Assert.NotNull(xlsxSummary);
        Assert.Equal(1, xlsxSummary.TotalImported);

        // =========================================================================
        // 12. Batch History Query
        // =========================================================================
        _output.WriteLine("=== 12. Batch History Query ===");
        var batchesRes = await ownerClient.GetAsync($"/api/inventory/import/batches?branchId={b1.Id}");
        Assert.Equal(HttpStatusCode.OK, batchesRes.StatusCode);
        var batches = await batchesRes.Content.ReadFromJsonAsync<List<InventoryImportBatchDto>>();
        Assert.NotNull(batches);
        Assert.True(batches.Count >= 2);
        Assert.Contains(batches, b => b.Id == validCsvValidation.BatchId && b.StatusName == "COMPLETED");

        // =========================================================================
        // 13. RBAC & Branch Authorization Enforcement
        // =========================================================================
        _output.WriteLine("=== 13. RBAC & Branch Authorization Enforcement ===");
        // Cashier role has NO INVENTORY_BULK_IMPORT permission
        var cashierRoleRes = await ownerClient.PostAsJsonAsync("/api/roles", new CreateRoleRequest("Test M7 Cashier Role", "Cashier", new List<Guid>()));
        var cashierRole = await cashierRoleRes.Content.ReadFromJsonAsync<RoleDto>();

        var cashierUserRes = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            "test_cashier_m7", "cashier_m7@saranga.lk", "CashierM7@2026#Secure", "Test Cashier M7",
            new List<Guid> { cashierRole!.Id }, new List<Guid> { b1.Id }, b1.Id, false
        ));
        Assert.Equal(HttpStatusCode.Created, cashierUserRes.StatusCode);

        // Login as Cashier
        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_cashier_m7", "CashierM7@2026#Secure"));
        var cashierAuth = await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>();

        var cashierClient = _factory.CreateClient();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierAuth!.AccessToken);

        // Cashier attempt to validate -> 403 Forbidden
        using (var content = new MultipartFormDataContent())
        {
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(validCsv.ToString()));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(fileContent, "file", "cashier_attempt.csv");

            var cashierValidateRes = await cashierClient.PostAsync($"/api/inventory/import/validate?branchId={b1.Id}", content);
            Assert.Equal(HttpStatusCode.Forbidden, cashierValidateRes.StatusCode);
        }

        // Cashier attempt to confirm -> 403 Forbidden
        var cashierConfirmRes = await cashierClient.PostAsJsonAsync("/api/inventory/import/confirm", new ConfirmBulkImportRequest(xlsxValidation.BatchId));
        Assert.Equal(HttpStatusCode.Forbidden, cashierConfirmRes.StatusCode);
        _output.WriteLine("Verified: Users without INVENTORY_BULK_IMPORT permission are rejected with 403 Forbidden.");

        // =========================================================================
        // 14. Audit Trail Verification
        // =========================================================================
        _output.WriteLine("=== 14. Audit Trail Verification ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var batchAudit = await db.AuditLogs
                .Where(a => a.EntityName == "InventoryImportBatch" && (a.Action == "BATCH_CONFIRMED" || a.Action == "BATCH_VALIDATED" || a.Action == "BATCH_VALIDATION_FAILED"))
                .ToListAsync();

            Assert.NotEmpty(batchAudit);
            Assert.Contains(batchAudit, a => a.Action == "BATCH_CONFIRMED");
            Assert.Contains(batchAudit, a => a.Action == "BATCH_VALIDATION_FAILED");

            var allItemAudits = await db.AuditLogs
                .Where(a => a.EntityName == "InventoryItem" && a.Action == "CREATE")
                .ToListAsync();

            var itemAudits = allItemAudits
                .Where(a => a.NewValues != null && a.NewValues.Contains("ImportBatchId"))
                .ToList();

            Assert.True(itemAudits.Count >= 3); // 2 from CSV, 1 from Excel
        }
        _output.WriteLine("Verified: Comprehensive audit entries created for import batches and created inventory items.");

        _output.WriteLine("=== ALL MODULE 7 BULK IMPORT TESTS COMPLETED SUCCESSFULLY ===");
    }
}

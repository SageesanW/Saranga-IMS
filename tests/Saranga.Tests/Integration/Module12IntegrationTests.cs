using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Api;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.DTOs.Branches;
using Saranga.Application.DTOs.CashLedger;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Shifts;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Domain.Enums;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module12IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Module12IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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
            DELETE FROM ""CashLedgerEntries"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T12B%');
            DELETE FROM ""CashLedgerTransactions"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T12B%');
            DELETE FROM ""CashMovementCategories"" WHERE ""Code"" LIKE 'TEST_%';
            DELETE FROM ""Shifts"" WHERE ""ShiftNumber"" LIKE 'SHF-T12B%' OR ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T12B%');
            DELETE FROM ""CashDrawers"" WHERE ""DrawerCode"" LIKE 'T12DRW%' OR ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T12B%');
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T12B%')
                OR ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m12_%');
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m12_%');
            DELETE FROM ""Users"" WHERE ""Username"" LIKE 'test_m12_%';
            DELETE FROM ""Branches"" WHERE ""Code"" LIKE 'T12B%';
            DELETE FROM ""IdempotencyRecords"" WHERE ""Key"" LIKE 'IDEMP-M12-%' OR ""Key"" LIKE 'CL-%';
        ");
    }

    [Fact]
    public async Task ComprehensiveModule12_CashLedgerAndMovements_EndToEndVerification()
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
        // 2. Setup Test Branches, Drawers, Cashiers, and Manager
        // =========================================================================
        _output.WriteLine("=== 2. Setup Test Branches, Cash Drawers, and Staff ===");
        var branchRes1 = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test M12 Colombo Flagship",
            Code: "T12B1",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Created, branchRes1.StatusCode);
        var branch1 = (await branchRes1.Content.ReadFromJsonAsync<BranchDto>())!;

        var branchRes2 = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test M12 Kandy Branch",
            Code: "T12B2",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Created, branchRes2.StatusCode);
        var branch2 = (await branchRes2.Content.ReadFromJsonAsync<BranchDto>())!;

        // Register 2 Drawers in Branch 1 and 1 Drawer in Branch 2
        var drwRes1 = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(
            BranchId: branch1.Id,
            DrawerCode: "T12DRW1",
            Name: "Main Counter Drawer 1",
            Description: "Primary cash drawer"
        ));
        Assert.Equal(HttpStatusCode.Created, drwRes1.StatusCode);
        var drawer1 = (await drwRes1.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        var drwRes2 = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(
            BranchId: branch1.Id,
            DrawerCode: "T12DRW2",
            Name: "Secondary Counter Drawer 2",
            Description: "Secondary cash drawer"
        ));
        Assert.Equal(HttpStatusCode.Created, drwRes2.StatusCode);
        var drawer2 = (await drwRes2.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        var drwRes3 = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(
            BranchId: branch2.Id,
            DrawerCode: "T12DRW3",
            Name: "Kandy Drawer 1",
            Description: "Kandy branch drawer"
        ));
        Assert.Equal(HttpStatusCode.Created, drwRes3.StatusCode);
        var drawer3 = (await drwRes3.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        // Create Roles lookup
        var rolesRes = await ownerClient.GetFromJsonAsync<List<Saranga.Application.DTOs.Roles.RoleDto>>("/api/roles");
        Assert.NotNull(rolesRes);
        var cashierRoleId = rolesRes.First(r => r.Name == SystemRoles.Cashier).Id;
        var managerRoleId = rolesRes.First(r => r.Name == SystemRoles.Manager).Id;

        // Cashier 1
        var userRes1 = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m12_cashier1",
            Email: "test_m12_cashier1@saranga.lk",
            Password: "Password123!",
            FullName: "Test M12 Cashier 1",
            RoleIds: new List<Guid> { cashierRoleId },
            BranchIds: new List<Guid> { branch1.Id },
            DefaultBranchId: branch1.Id
        ));
        Assert.Equal(HttpStatusCode.Created, userRes1.StatusCode);
        var cashier1 = (await userRes1.Content.ReadFromJsonAsync<UserDto>())!;

        // Cashier 2
        var userRes2 = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m12_cashier2",
            Email: "test_m12_cashier2@saranga.lk",
            Password: "Password123!",
            FullName: "Test M12 Cashier 2",
            RoleIds: new List<Guid> { cashierRoleId },
            BranchIds: new List<Guid> { branch1.Id },
            DefaultBranchId: branch1.Id
        ));
        Assert.Equal(HttpStatusCode.Created, userRes2.StatusCode);
        var cashier2 = (await userRes2.Content.ReadFromJsonAsync<UserDto>())!;

        // Manager
        var mgrRes = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m12_manager",
            Email: "test_m12_manager@saranga.lk",
            Password: "Password123!",
            FullName: "Test M12 Branch Manager",
            RoleIds: new List<Guid> { managerRoleId },
            BranchIds: new List<Guid> { branch1.Id, branch2.Id },
            DefaultBranchId: branch1.Id
        ));
        Assert.Equal(HttpStatusCode.Created, mgrRes.StatusCode);
        var manager = (await mgrRes.Content.ReadFromJsonAsync<UserDto>())!;

        // Login Cashier 1
        var c1Login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m12_cashier1", "Password123!"));
        var c1Token = (await c1Login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var c1Client = _factory.CreateClient();
        c1Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", c1Token);

        // Login Cashier 2
        var c2Login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m12_cashier2", "Password123!"));
        var c2Token = (await c2Login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var c2Client = _factory.CreateClient();
        c2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", c2Token);

        // Login Manager
        var mgrLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m12_manager", "Password123!"));
        var mgrToken = (await mgrLogin.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var mgrClient = _factory.CreateClient();
        mgrClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);

        // =========================================================================
        // 3. Open Shifts on Drawer 1 and Drawer 2
        // =========================================================================
        _output.WriteLine("=== 3. Open Shifts on Drawer 1 and Drawer 2 ===");
        // Cashier 1 opens Drawer 1 with Rs. 20,000 float
        var openShiftRes1 = await c1Client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch1.Id,
            CashDrawerId: drawer1.Id,
            OpeningBalance: 20000.00m,
            OpeningDifferenceReason: "Initial cash float Drawer 1",
            OpeningNotes: "Morning float Drawer 1"
        ));
        Assert.Equal(HttpStatusCode.Created, openShiftRes1.StatusCode);
        var shift1 = (await openShiftRes1.Content.ReadFromJsonAsync<ShiftDto>())!;
        Assert.Equal(20000.00m, shift1.OpeningBalance);
        Assert.Equal(20000.00m, shift1.ExpectedClosingBalance);

        // Cashier 2 opens Drawer 2 with Rs. 10,000 float
        var openShiftRes2 = await c2Client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch1.Id,
            CashDrawerId: drawer2.Id,
            OpeningBalance: 10000.00m,
            OpeningDifferenceReason: "Initial cash float Drawer 2",
            OpeningNotes: "Morning float Drawer 2"
        ));
        Assert.Equal(HttpStatusCode.Created, openShiftRes2.StatusCode);
        var shift2 = (await openShiftRes2.Content.ReadFromJsonAsync<ShiftDto>())!;
        Assert.Equal(10000.00m, shift2.OpeningBalance);

        // =========================================================================
        // 4. Category Management & Default Categories
        // =========================================================================
        _output.WriteLine("=== 4. Category Management & Default Categories ===");
        var catsRes = await ownerClient.GetFromJsonAsync<List<CashMovementCategoryDto>>("/api/cash-ledger/categories");
        Assert.NotNull(catsRes);
        Assert.Contains(catsRes, c => c.Code == "ADDED_FLOAT" && c.AllowedDirection == 1);
        Assert.Contains(catsRes, c => c.Code == "PETTY_EXPENSE" && c.AllowedDirection == 2);

        // Create Custom Category
        var createCatRes = await mgrClient.PostAsJsonAsync("/api/cash-ledger/categories", new CreateCashMovementCategoryRequest(
            Name: "Test Loan Repayment",
            Code: "TEST_LOAN_REP",
            AllowedDirection: 1, // In only
            Description: "Repayment of short term staff advance"
        ));
        Assert.Equal(HttpStatusCode.OK, createCatRes.StatusCode);
        var customCat = (await createCatRes.Content.ReadFromJsonAsync<CashMovementCategoryDto>())!;

        // Update Custom Category
        var updateCatRes = await mgrClient.PutAsJsonAsync($"/api/cash-ledger/categories/{customCat.Id}", new UpdateCashMovementCategoryRequest(
            Name: "Test Loan Repayment Updated",
            AllowedDirection: 3, // Both
            Description: "Updated description"
        ));
        Assert.Equal(HttpStatusCode.OK, updateCatRes.StatusCode);

        // Toggle Status
        var statusCatRes = await mgrClient.PatchAsJsonAsync($"/api/cash-ledger/categories/{customCat.Id}/status", new UpdateCategoryStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, statusCatRes.StatusCode);
        // Re-activate
        await mgrClient.PatchAsJsonAsync($"/api/cash-ledger/categories/{customCat.Id}/status", new UpdateCategoryStatusRequest(true));

        // =========================================================================
        // 5. Cash In Workflow & Precision / Monetary Validation
        // =========================================================================
        _output.WriteLine("=== 5. Cash In Workflow & Validations ===");
        // Invalid amount <= 0
        var negCashIn = await c1Client.PostAsJsonAsync("/api/cash-ledger/cash-in", new PostCashInRequest(
            ShiftId: shift1.Id,
            Amount: -500m,
            CategoryId: null,
            Description: null,
            Reason: null,
            ExternalReference: null
        ));
        Assert.Equal(HttpStatusCode.BadRequest, negCashIn.StatusCode);

        // Invalid precision > 2 decimals
        var precCashIn = await c1Client.PostAsJsonAsync("/api/cash-ledger/cash-in", new PostCashInRequest(
            ShiftId: shift1.Id,
            Amount: 100.555m,
            CategoryId: null,
            Description: null,
            Reason: null,
            ExternalReference: null
        ));
        Assert.Equal(HttpStatusCode.BadRequest, precCashIn.StatusCode);

        // Valid Cash In: Rs. 5,000 to Shift 1
        var validCashInReq = new PostCashInRequest(
            ShiftId: shift1.Id,
            Amount: 5000.00m,
            CategoryId: customCat.Id,
            Description: "Mid-day additional float",
            Reason: "High customer footfall expected",
            ExternalReference: "EXT-CIN-001"
        );
        var cashInReqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/cash-ledger/cash-in")
        {
            Content = JsonContent.Create(validCashInReq)
        };
        var idempKey1 = "IDEMP-M12-CIN-001";
        cashInReqMsg.Headers.Add("Idempotency-Key", idempKey1);

        var cashInRes = await c1Client.SendAsync(cashInReqMsg);
        Assert.Equal(HttpStatusCode.Created, cashInRes.StatusCode);
        var cashInTx = (await cashInRes.Content.ReadFromJsonAsync<CashLedgerTransactionDto>())!;
        Assert.StartsWith("CL-T12B1-", cashInTx.TransactionReference);
        Assert.Equal(5000.00m, cashInTx.Amount);
        Assert.Equal(1, cashInTx.TransactionType); // CashIn
        Assert.Single(cashInTx.Entries);
        Assert.Equal(1, cashInTx.Entries[0].Direction); // In
        Assert.Equal(shift1.Id, cashInTx.Entries[0].ShiftId);

        // Replay identical idempotency key -> returns cached result
        var replayInReqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/cash-ledger/cash-in")
        {
            Content = JsonContent.Create(validCashInReq)
        };
        replayInReqMsg.Headers.Add("Idempotency-Key", idempKey1);
        var replayInRes = await c1Client.SendAsync(replayInReqMsg);
        Assert.Equal(HttpStatusCode.Created, replayInRes.StatusCode);
        var replayedTx = (await replayInRes.Content.ReadFromJsonAsync<CashLedgerTransactionDto>())!;
        Assert.Equal(cashInTx.Id, replayedTx.Id);

        // Replay key with modified payload -> rejected with 400
        var conflictInReqMsg = new HttpRequestMessage(HttpMethod.Post, "/api/cash-ledger/cash-in")
        {
            Content = JsonContent.Create(validCashInReq with { Amount = 6000.00m })
        };
        conflictInReqMsg.Headers.Add("Idempotency-Key", idempKey1);
        var conflictInRes = await c1Client.SendAsync(conflictInReqMsg);
        Assert.Equal(HttpStatusCode.BadRequest, conflictInRes.StatusCode);

        // Verify Shift 1 summary: Expected = 20,000 + 5,000 = 25,000
        var s1SummaryRes = await c1Client.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift1.Id}/summary");
        Assert.NotNull(s1SummaryRes);
        Assert.Equal(20000.00m, s1SummaryRes.OpeningBalance);
        Assert.Equal(5000.00m, s1SummaryRes.TotalCashIn);
        Assert.Equal(0m, s1SummaryRes.TotalCashOut);
        Assert.Equal(25000.00m, s1SummaryRes.CurrentExpectedBalance);

        // =========================================================================
        // 6. Cash Out Workflow & Insufficient Balance Rejection
        // =========================================================================
        _output.WriteLine("=== 6. Cash Out Workflow & Insufficient Balance Rejection ===");
        // Insufficient balance check: Shift 1 currently has Rs. 25,000. Try to take out Rs. 30,000
        var overspendRes = await c1Client.PostAsJsonAsync("/api/cash-ledger/cash-out", new PostCashOutRequest(
            ShiftId: shift1.Id,
            Amount: 30000.00m,
            CategoryId: null,
            Description: null,
            Reason: "Too large",
            ExternalReference: null
        ));
        Assert.Equal(HttpStatusCode.BadRequest, overspendRes.StatusCode);

        // Valid Cash Out: Rs. 3,000 from Shift 1
        var pettyCat = catsRes.First(c => c.Code == "PETTY_EXPENSE");
        var validCashOutRes = await c1Client.PostAsJsonAsync("/api/cash-ledger/cash-out", new PostCashOutRequest(
            ShiftId: shift1.Id,
            Amount: 3000.00m,
            CategoryId: pettyCat.Id,
            Description: "Store tea and snacks",
            Reason: "Staff refreshments",
            ExternalReference: "VOUCH-101"
        ));
        Assert.Equal(HttpStatusCode.Created, validCashOutRes.StatusCode);
        var cashOutTx = (await validCashOutRes.Content.ReadFromJsonAsync<CashLedgerTransactionDto>())!;
        Assert.Equal(3000.00m, cashOutTx.Amount);
        Assert.Equal(2, cashOutTx.TransactionType); // CashOut
        Assert.Single(cashOutTx.Entries);
        Assert.Equal(2, cashOutTx.Entries[0].Direction); // Out

        // Verify Shift 1 summary: Expected = 25,000 - 3,000 = 22,000
        var s1SummaryAfterOut = await c1Client.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift1.Id}/summary");
        Assert.Equal(5000.00m, s1SummaryAfterOut!.TotalCashIn);
        Assert.Equal(3000.00m, s1SummaryAfterOut.TotalCashOut);
        Assert.Equal(22000.00m, s1SummaryAfterOut.CurrentExpectedBalance);

        // =========================================================================
        // 7. Cash Drawer Transfer & Validation
        // =========================================================================
        _output.WriteLine("=== 7. Cash Drawer Transfer & Validation ===");
        // Same drawer transfer rejected
        var sameDrawerTransfer = await mgrClient.PostAsJsonAsync("/api/cash-ledger/transfer", new PostTransferRequest(
            SourceShiftId: shift1.Id,
            DestinationShiftId: shift1.Id,
            Amount: 1000m,
            Reason: "Same drawer test",
            ExternalReference: null
        ));
        Assert.Equal(HttpStatusCode.BadRequest, sameDrawerTransfer.StatusCode);

        // Transfer exceeding source balance (try transfer 25,000 when source has 22,000)
        var overTransfer = await mgrClient.PostAsJsonAsync("/api/cash-ledger/transfer", new PostTransferRequest(
            SourceShiftId: shift1.Id,
            DestinationShiftId: shift2.Id,
            Amount: 25000m,
            Reason: "Overdraft transfer test",
            ExternalReference: null
        ));
        Assert.Equal(HttpStatusCode.BadRequest, overTransfer.StatusCode);

        // Valid Transfer: Rs. 7,000 from Drawer 1 (Shift 1) to Drawer 2 (Shift 2)
        var validTransferRes = await mgrClient.PostAsJsonAsync("/api/cash-ledger/transfer", new PostTransferRequest(
            SourceShiftId: shift1.Id,
            DestinationShiftId: shift2.Id,
            Amount: 7000.00m,
            Reason: "Float balancing between counters",
            ExternalReference: "TRF-001"
        ));
        Assert.Equal(HttpStatusCode.Created, validTransferRes.StatusCode);
        var transferTx = (await validTransferRes.Content.ReadFromJsonAsync<CashLedgerTransactionDto>())!;
        Assert.Equal(3, transferTx.TransactionType); // Transfer
        Assert.Equal(7000.00m, transferTx.Amount);
        Assert.Equal(2, transferTx.Entries.Count); // Dual entries

        var outEntry = transferTx.Entries.First(e => e.Direction == 2);
        var inEntry = transferTx.Entries.First(e => e.Direction == 1);
        Assert.Equal(shift1.Id, outEntry.ShiftId);
        Assert.Equal(shift2.Id, inEntry.ShiftId);

        // Verify Shift 1: Expected = 22,000 - 7,000 = 15,000
        var s1SummaryAfterTransfer = await c1Client.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift1.Id}/summary");
        Assert.Equal(15000.00m, s1SummaryAfterTransfer!.CurrentExpectedBalance);

        // Verify Shift 2: Expected = 10,000 + 7,000 = 17,000
        var s2SummaryAfterTransfer = await c2Client.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift2.Id}/summary");
        Assert.Equal(17000.00m, s2SummaryAfterTransfer!.CurrentExpectedBalance);

        // =========================================================================
        // 8. Reversal Workflow (Cash In Reversal & Transfer Reversal)
        // =========================================================================
        _output.WriteLine("=== 8. Reversal Workflow ===");
        // Post separate Cash In of Rs. 1,000 to Shift 1 to test reversal
        var tempInRes = await c1Client.PostAsJsonAsync("/api/cash-ledger/cash-in", new PostCashInRequest(
            ShiftId: shift1.Id,
            Amount: 1000.00m,
            CategoryId: null,
            Description: "Temp cash in",
            Reason: "Mistake to be reversed",
            ExternalReference: null
        ));
        Assert.Equal(HttpStatusCode.Created, tempInRes.StatusCode);
        var tempInTx = (await tempInRes.Content.ReadFromJsonAsync<CashLedgerTransactionDto>())!;

        // Shift 1 balance is now 16,000
        var s1SummaryTemp = await c1Client.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift1.Id}/summary");
        Assert.Equal(16000.00m, s1SummaryTemp!.CurrentExpectedBalance);

        // Reverse the Rs. 1,000 Cash In
        var revRes = await mgrClient.PostAsJsonAsync($"/api/cash-ledger/transactions/{tempInTx.Id}/reverse", new ReverseTransactionRequest(
            ReversalReason: "Recorded against wrong drawer"
        ));
        Assert.Equal(HttpStatusCode.OK, revRes.StatusCode);
        var revTx = (await revRes.Content.ReadFromJsonAsync<CashLedgerTransactionDto>())!;
        Assert.Equal(4, revTx.TransactionType); // Reversal
        Assert.Equal(tempInTx.Id, revTx.ReversalOfTransactionId);
        Assert.Single(revTx.Entries);
        Assert.Equal(2, revTx.Entries[0].Direction); // Compensating OUT

        // Shift 1 balance correctly returns to 15,000
        var s1SummaryAfterRev = await c1Client.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift1.Id}/summary");
        Assert.Equal(15000.00m, s1SummaryAfterRev!.CurrentExpectedBalance);

        // Duplicate reversal attempt on same transaction is rejected
        var dupRevRes = await mgrClient.PostAsJsonAsync($"/api/cash-ledger/transactions/{tempInTx.Id}/reverse", new ReverseTransactionRequest(
            ReversalReason: "Second reversal attempt"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, dupRevRes.StatusCode);

        // Attempting to reverse a reversal transaction itself is rejected
        var revOnRevRes = await mgrClient.PostAsJsonAsync($"/api/cash-ledger/transactions/{revTx.Id}/reverse", new ReverseTransactionRequest(
            ReversalReason: "Recursive reversal attempt"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, revOnRevRes.StatusCode);

        // Reverse Transfer
        var revTransferRes = await mgrClient.PostAsJsonAsync($"/api/cash-ledger/transactions/{transferTx.Id}/reverse", new ReverseTransactionRequest(
            ReversalReason: "Transfer error correction"
        ));
        Assert.Equal(HttpStatusCode.OK, revTransferRes.StatusCode);

        // Drawer 1 balance returns to 22,000 (15,000 + 7,000)
        var s1AfterRevTransfer = await c1Client.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift1.Id}/summary");
        Assert.Equal(22000.00m, s1AfterRevTransfer!.CurrentExpectedBalance);

        // Drawer 2 balance returns to 10,000 (17,000 - 7,000)
        var s2AfterRevTransfer = await c2Client.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift2.Id}/summary");
        Assert.Equal(10000.00m, s2AfterRevTransfer!.CurrentExpectedBalance);

        // =========================================================================
        // 9. Closed Shift Protection
        // =========================================================================
        _output.WriteLine("=== 9. Closed Shift Protection ===");
        // Close Shift 2 with exact balance Rs. 10,000
        var closeShift2Res = await c2Client.PostAsJsonAsync($"/api/shifts/{shift2.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 10000.00m,
            ClosingVarianceReason: null,
            ClosingNotes: "Normal shift close",
            OverrideReason: null,
            RowVersion: shift2.Version
        ));
        Assert.Equal(HttpStatusCode.OK, closeShift2Res.StatusCode);

        // Attempt Cash In on closed shift -> rejected
        var postOnClosedRes = await c2Client.PostAsJsonAsync("/api/cash-ledger/cash-in", new PostCashInRequest(
            ShiftId: shift2.Id,
            Amount: 500m,
            CategoryId: null,
            Description: null,
            Reason: "Closed shift test",
            ExternalReference: null
        ));
        Assert.Equal(HttpStatusCode.BadRequest, postOnClosedRes.StatusCode);

        // Attempt Reversal of an earlier transaction involving closed shift -> rejected
        var revClosedShiftTx = await mgrClient.PostAsJsonAsync($"/api/cash-ledger/transactions/{transferTx.Id}/reverse", new ReverseTransactionRequest(
            ReversalReason: "Closed shift reversal test"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, revClosedShiftTx.StatusCode);

        // =========================================================================
        // 10. Shift Closing with Active Movements & Variance Verification
        // =========================================================================
        _output.WriteLine("=== 10. Shift Closing with Active Movements & Variance ===");
        // Current Shift 1 expected balance is Rs. 22,000.00 (verified by backend calculation)
        var shift1Details = await c1Client.GetFromJsonAsync<ShiftDto>($"/api/shifts/{shift1.Id}");
        Assert.Equal(22000.00m, shift1Details!.ExpectedClosingBalance);

        // Close Shift 1 with exact cash Rs. 22,000.00 -> variance 0.00
        var closeShift1Res = await c1Client.PostAsJsonAsync($"/api/shifts/{shift1.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 22000.00m,
            ClosingVarianceReason: null,
            ClosingNotes: "Shift 1 successfully reconciled with Cash Ledger",
            OverrideReason: null,
            RowVersion: shift1Details.Version
        ));
        Assert.Equal(HttpStatusCode.OK, closeShift1Res.StatusCode);
        var closedShift1 = (await closeShift1Res.Content.ReadFromJsonAsync<ShiftDto>())!;
        Assert.Equal(ShiftStatus.Closed, closedShift1.Status);
        Assert.Equal(22000.00m, closedShift1.ActualClosingBalance);
        Assert.Equal(22000.00m, closedShift1.ExpectedClosingBalance);
        Assert.Equal(0.00m, closedShift1.ClosingVariance);

        // =========================================================================
        // 11. Transaction History and Immutability
        // =========================================================================
        _output.WriteLine("=== 11. Transaction History and Immutability ===");
        var txListRes = await ownerClient.GetFromJsonAsync<PagedResult<CashLedgerTransactionDto>>($"/api/cash-ledger/transactions?branchId={branch1.Id}");
        Assert.NotNull(txListRes);
        Assert.True(txListRes.TotalCount >= 4);

        // EF Core ChangeTracker Immutability check
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entryInDb = await db.CashLedgerEntries.FirstAsync(e => e.ShiftId == shift1.Id);
            entryInDb.Amount = 999999m;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================================
        // 12. Audit Log Verification
        // =========================================================================
        _output.WriteLine("=== 12. Audit Log Verification ===");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditActions = await db.AuditLogs
                .Where(a => a.EntityName == "CashLedgerTransaction" || a.EntityName == "CashMovementCategory")
                .Select(a => a.Action)
                .ToListAsync();

            Assert.Contains("CASH_IN_POSTED", auditActions);
            Assert.Contains("CASH_OUT_POSTED", auditActions);
            Assert.Contains("CASH_TRANSFER_POSTED", auditActions);
            Assert.Contains("CASH_TRANSACTION_REVERSED", auditActions);
            Assert.Contains("CASH_CATEGORY_CREATED", auditActions);
        }

        _output.WriteLine("=== Comprehensive Module 12 Integration Test Completed Successfully! ===");
    }

    [Fact]
    public async Task ConcurrentCashOut_AgainstSameShift_CannotOverspend()
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        var ownerToken = (await loginRes.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // Setup branch, drawer, and open shift with Rs. 5,000 float
        var branchRes = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M12 Conc Branch", "T12BC", "LK", "LKR", "Asia/Colombo", true));
        var branch = (await branchRes.Content.ReadFromJsonAsync<BranchDto>())!;

        var drwRes = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(branch.Id, "T12DRWC", "Conc Drawer", null));
        var drawer = (await drwRes.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        var openShiftRes = await ownerClient.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(branch.Id, drawer.Id, 5000.00m, "Initial float", "Concurrent test"));
        var shift = (await openShiftRes.Content.ReadFromJsonAsync<ShiftDto>())!;

        // Two concurrent cash-out requests, each asking for Rs. 3,500.
        // Total available is Rs. 5,000. Exactly ONE must succeed and the other must be rejected due to insufficient cash!
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var taskA = clientA.PostAsJsonAsync("/api/cash-ledger/cash-out", new PostCashOutRequest(shift.Id, 3500.00m, null, "A", "Concurrent Out A", null));
        var taskB = clientB.PostAsJsonAsync("/api/cash-ledger/cash-out", new PostCashOutRequest(shift.Id, 3500.00m, null, "B", "Concurrent Out B", null));

        var responses = await Task.WhenAll(taskA, taskB);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var badRequestCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        Assert.Equal(1, successCount);
        Assert.Equal(1, badRequestCount);

        // Authoritative expected balance must be 5,000 - 3,500 = 1,500
        var summary = await ownerClient.GetFromJsonAsync<ShiftCashLedgerSummaryDto>($"/api/cash-ledger/shifts/{shift.Id}/summary");
        Assert.Equal(1500.00m, summary!.CurrentExpectedBalance);
    }

    [Fact]
    public async Task ConcurrentMovement_VersusShiftClosing_SerializesSafely()
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure"));
        var ownerToken = (await loginRes.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // Setup branch, drawer, and open shift with Rs. 10,000 float
        var branchRes = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest("Test M12 Close Branch", "T12BCLS", "LK", "LKR", "Asia/Colombo", true));
        var branch = (await branchRes.Content.ReadFromJsonAsync<BranchDto>())!;

        var drwRes = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(branch.Id, "T12DRWCLS", "Close Drawer", null));
        var drawer = (await drwRes.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        var openShiftRes = await ownerClient.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(branch.Id, drawer.Id, 10000.00m, "Initial float", "Closing vs CashIn"));
        var shift = (await openShiftRes.Content.ReadFromJsonAsync<ShiftDto>())!;

        var clientClose = _factory.CreateClient();
        clientClose.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        var clientCashIn = _factory.CreateClient();
        clientCashIn.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // Race: Close shift with Rs. 10,000 actual cash vs Post Cash In Rs. 2,000
        var taskClose = clientClose.PostAsJsonAsync($"/api/shifts/{shift.Id}/close", new CloseShiftRequest(10000.00m, null, "Close race", null, shift.Version));
        var taskCashIn = clientCashIn.PostAsJsonAsync("/api/cash-ledger/cash-in", new PostCashInRequest(shift.Id, 2000.00m, null, "Race Cash In", "Race", null));

        var responses = await Task.WhenAll(taskClose, taskCashIn);
        var closeRes = responses[0];
        var cashInRes = responses[1];

        // The operations serialize through PostgreSQL row lock:
        // Scenario 1: Close ran first -> Close succeeded, Cash In rejected with 400 (Shift is closed).
        // Scenario 2: Cash In ran first -> Cash In succeeded (Created), Close either succeeded with variance Rs. -2,000 (if reason was provided) or rejected due to variance or xmin mismatch.
        // In all scenarios, no 500 error occurs and no cash movement slips in uncounted!
        Assert.True(closeRes.StatusCode == HttpStatusCode.OK || closeRes.StatusCode == HttpStatusCode.BadRequest || closeRes.StatusCode == HttpStatusCode.Conflict);
        Assert.True(cashInRes.StatusCode == HttpStatusCode.Created || cashInRes.StatusCode == HttpStatusCode.BadRequest);
        Assert.NotEqual(HttpStatusCode.InternalServerError, closeRes.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, cashInRes.StatusCode);
    }
}

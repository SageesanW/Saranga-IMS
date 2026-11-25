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
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Shifts;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Domain.Enums;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module11IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Module11IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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
            DELETE FROM ""Shifts"" WHERE ""ShiftNumber"" LIKE 'SHF-T11B1-%' OR ""OpeningDifferenceReason"" LIKE 'Test M11 %';
            DELETE FROM ""CashDrawers"" WHERE ""DrawerCode"" LIKE 'T11DRW%' OR ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" = 'T11B1');
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" = 'T11B1')
                OR ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m11_%');
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m11_%');
            DELETE FROM ""Users"" WHERE ""Username"" LIKE 'test_m11_%';
            DELETE FROM ""Branches"" WHERE ""Code"" = 'T11B1';
        ");
    }

    [Fact]
    public async Task ComprehensiveModule11_ShiftOpeningAndClosing_EndToEndVerification()
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
        // 2. Setup Test Branch, Drawers, Cashiers, and Manager
        // =========================================================================
        _output.WriteLine("=== 2. Setup Test Branch, Cash Drawers, and Staff ===");
        var branchRes = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test M11 Colombo Flagship",
            Code: "T11B1",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Created, branchRes.StatusCode);
        var branch = (await branchRes.Content.ReadFromJsonAsync<BranchDto>())!;

        // Register 2 Cash Drawers via dedicated CASH_DRAWER_MANAGE endpoint
        var drawer1Res = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(
            BranchId: branch.Id,
            DrawerCode: "T11DRW1",
            Name: "Main Cash Drawer 1",
            Description: "Counter Till 1"
        ));
        Assert.Equal(HttpStatusCode.Created, drawer1Res.StatusCode);
        var drawer1 = (await drawer1Res.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        var drawer2Res = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(
            BranchId: branch.Id,
            DrawerCode: "T11DRW2",
            Name: "Express Cash Drawer 2",
            Description: "Counter Till 2"
        ));
        Assert.Equal(HttpStatusCode.Created, drawer2Res.StatusCode);
        var drawer2 = (await drawer2Res.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        // Verify Case-Insensitive Drawer Code Uniqueness within same branch
        var duplicateDrawerRes = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(
            BranchId: branch.Id,
            DrawerCode: "t11drw1", // lower-case duplicate
            Name: "Duplicate Drawer Code"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateDrawerRes.StatusCode);
        var dupDrawerErr = await duplicateDrawerRes.Content.ReadAsStringAsync();
        Assert.Contains("already exists", dupDrawerErr, StringComparison.OrdinalIgnoreCase);

        // Get Role IDs
        var rolesRes = await ownerClient.GetFromJsonAsync<List<Saranga.Application.DTOs.Roles.RoleDto>>("/api/roles");
        Assert.NotNull(rolesRes);
        var cashierRole = rolesRes.First(r => r.Name == SystemRoles.Cashier);
        var managerRole = rolesRes.First(r => r.Name == SystemRoles.Manager);

        // Create Cashier 1
        var cashier1Res = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m11_cashier1",
            Email: "test_m11_cashier1@saranga.lk",
            Password: "Cashier@2026#Secure",
            FullName: "M11 Cashier One",
            RoleIds: new List<Guid> { cashierRole.Id },
            BranchIds: new List<Guid> { branch.Id },
            DefaultBranchId: branch.Id
        ));
        Assert.Equal(HttpStatusCode.Created, cashier1Res.StatusCode);
        var cashier1User = (await cashier1Res.Content.ReadFromJsonAsync<UserDto>())!;

        // Create Cashier 2
        var cashier2Res = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m11_cashier2",
            Email: "test_m11_cashier2@saranga.lk",
            Password: "Cashier@2026#Secure",
            FullName: "M11 Cashier Two",
            RoleIds: new List<Guid> { cashierRole.Id },
            BranchIds: new List<Guid> { branch.Id },
            DefaultBranchId: branch.Id
        ));
        Assert.Equal(HttpStatusCode.Created, cashier2Res.StatusCode);
        var cashier2User = (await cashier2Res.Content.ReadFromJsonAsync<UserDto>())!;

        // Create Manager (has SHIFT_OVERRIDE_CLOSE and CASH_DRAWER_MANAGE)
        var managerRes = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m11_manager",
            Email: "test_m11_manager@saranga.lk",
            Password: "Manager@2026#Secure",
            FullName: "M11 Branch Manager",
            RoleIds: new List<Guid> { managerRole.Id },
            BranchIds: new List<Guid> { branch.Id },
            DefaultBranchId: branch.Id
        ));
        Assert.Equal(HttpStatusCode.Created, managerRes.StatusCode);
        var managerUser = (await managerRes.Content.ReadFromJsonAsync<UserDto>())!;

        // Login Cashier 1
        var cashier1Login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m11_cashier1", "Cashier@2026#Secure"));
        var cashier1Token = (await cashier1Login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var cashier1Client = _factory.CreateClient();
        cashier1Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashier1Token);

        // Login Cashier 2
        var cashier2Login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m11_cashier2", "Cashier@2026#Secure"));
        var cashier2Token = (await cashier2Login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var cashier2Client = _factory.CreateClient();
        cashier2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashier2Token);

        // Login Manager
        var managerLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m11_manager", "Manager@2026#Secure"));
        var managerToken = (await managerLogin.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var managerClient = _factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        // =========================================================================
        // 3. Monetary Validation on Opening Balance
        // =========================================================================
        _output.WriteLine("=== 3. Monetary Input Validation on Opening Float ===");
        // Negative opening balance rejected
        var negativeOpenRes = await cashier1Client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer1.Id,
            OpeningBalance: -500.00m,
            OpeningDifferenceReason: "Negative test"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, negativeOpenRes.StatusCode);

        // Decimal precision > 2 rejected
        var invalidPrecisionOpenRes = await cashier1Client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer1.Id,
            OpeningBalance: 50000.123m,
            OpeningDifferenceReason: "Precision test"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, invalidPrecisionOpenRes.StatusCode);

        // =========================================================================
        // 4. First Shift: Defaults to Rs. 0.00 Expected Float & Persists Currency
        // =========================================================================
        _output.WriteLine("=== 4. Shift 1 Opening Float & Currency Persistence ===");
        var defaultsRes1 = await cashier1Client.GetFromJsonAsync<ShiftOpeningDefaultsDto>(
            $"/api/shifts/opening-defaults/{drawer1.Id}"
        );
        Assert.NotNull(defaultsRes1);
        Assert.Equal(0.00m, defaultsRes1.SystemExpectedOpeningBalance);
        Assert.Null(defaultsRes1.PreviousShiftNumber);

        // Open with mandatory reason
        var openReq1 = new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer1.Id,
            OpeningBalance: 50000.00m,
            OpeningDifferenceReason: "Test M11 Initial cash float from vault",
            OpeningNotes: "Verified note denominations"
        );
        var openRes1 = await cashier1Client.PostAsJsonAsync("/api/shifts/open", openReq1);
        Assert.Equal(HttpStatusCode.Created, openRes1.StatusCode);
        var shift1 = (await openRes1.Content.ReadFromJsonAsync<ShiftDto>())!;

        Assert.NotNull(shift1);
        Assert.StartsWith("SHF-T11B1-", shift1.ShiftNumber);
        Assert.Equal(ShiftStatus.Open, shift1.Status);
        Assert.Equal("LKR", shift1.CurrencyCode); // CurrencyCode persisted at opening time
        Assert.Equal(50000.00m, shift1.OpeningBalance);
        Assert.Equal(0.00m, shift1.SystemExpectedOpeningBalance);
        Assert.Equal(50000.00m, shift1.OpeningBalanceDifference);
        Assert.Equal(cashier1User.Id, shift1.CashierId);
        _output.WriteLine($"Shift 1 Opened: {shift1.ShiftNumber} in {shift1.CurrencyCode}");

        // =========================================================================
        // 5. Drawer Management: Cannot Deactivate Drawer while Open Shift Active
        // =========================================================================
        _output.WriteLine("=== 5. Cash Drawer Management Constraints ===");
        var deactivateBusyDrawerRes = await managerClient.PatchAsJsonAsync(
            $"/api/shifts/drawers/{drawer1.Id}/status",
            new UpdateCashDrawerStatusRequest(IsActive: false, Reason: "Maintenance")
        );
        Assert.Equal(HttpStatusCode.BadRequest, deactivateBusyDrawerRes.StatusCode);
        var deactErr = await deactivateBusyDrawerRes.Content.ReadAsStringAsync();
        Assert.Contains("open shift is in progress", deactErr, StringComparison.OrdinalIgnoreCase);

        // Ordinary cashier cannot manage cash drawers (needs CASH_DRAWER_MANAGE)
        var cashierDrawerManageRes = await cashier1Client.PostAsJsonAsync(
            "/api/shifts/drawers",
            new CreateCashDrawerRequest(branch.Id, "T11DRWX", "Unauthorized Drawer")
        );
        Assert.Equal(HttpStatusCode.Forbidden, cashierDrawerManageRes.StatusCode);

        // =========================================================================
        // 6. Ownership & Override Closing: Unauthorized Cashier Closing Blocked
        // =========================================================================
        _output.WriteLine("=== 6. Ownership & Unauthorized Close Prevention ===");
        // Cashier 2 attempts to close Cashier 1's active shift
        var unauthorizedCloseRes = await cashier2Client.PostAsJsonAsync($"/api/shifts/{shift1.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 52500.00m,
            ClosingVarianceReason: "Hijack close",
            RowVersion: shift1.Version
        ));
        Assert.Equal(HttpStatusCode.BadRequest, unauthorizedCloseRes.StatusCode);
        var unauthErr = await unauthorizedCloseRes.Content.ReadAsStringAsync();
        Assert.Contains("only close your own active shift", unauthErr, StringComparison.OrdinalIgnoreCase);

        // Management Override Closing without override reason must fail
        var managerCloseNoReasonRes = await managerClient.PostAsJsonAsync($"/api/shifts/{shift1.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 52500.00m,
            ClosingVarianceReason: "Variance reason",
            OverrideReason: null, // Missing mandatory override reason!
            RowVersion: shift1.Version
        ));
        Assert.Equal(HttpStatusCode.BadRequest, managerCloseNoReasonRes.StatusCode);
        var noReasonErr = await managerCloseNoReasonRes.Content.ReadAsStringAsync();
        Assert.Contains("override reason is mandatory", noReasonErr, StringComparison.OrdinalIgnoreCase);

        // Management Override Closing WITH override reason succeeds
        var managerCloseRes = await managerClient.PostAsJsonAsync($"/api/shifts/{shift1.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 52500.00m,
            ClosingVarianceReason: "Customer cash tip/surplus rounded into drawer",
            OverrideReason: "Cashier had medical emergency; manager closed shift",
            RowVersion: shift1.Version
        ));
        Assert.Equal(HttpStatusCode.OK, managerCloseRes.StatusCode);
        var closedShift1 = (await managerCloseRes.Content.ReadFromJsonAsync<ShiftDto>())!;

        Assert.Equal(ShiftStatus.Closed, closedShift1.Status);
        Assert.True(closedShift1.IsOverrideClose);
        Assert.Equal("Cashier had medical emergency; manager closed shift", closedShift1.OverrideReason);
        Assert.Equal(cashier1User.Id, closedShift1.CashierId); // Original cashier preserved!
        Assert.Equal(managerUser.Id, closedShift1.ClosedBy);    // Actual closer preserved!
        Assert.Equal(50000.00m, closedShift1.ExpectedClosingBalance);
        Assert.Equal(52500.00m, closedShift1.ActualClosingBalance);
        Assert.Equal(2500.00m, closedShift1.ClosingVariance);

        // =========================================================================
        // 7. Immutability: Closed Shifts Cannot Be Re-Closed
        // =========================================================================
        _output.WriteLine("=== 7. Immutability of Closed Shifts ===");
        var recloseRes = await cashier1Client.PostAsJsonAsync($"/api/shifts/{shift1.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 50000.00m,
            ClosingVarianceReason: null,
            RowVersion: closedShift1.Version
        ));
        Assert.Equal(HttpStatusCode.BadRequest, recloseRes.StatusCode);
        var recloseErr = await recloseRes.Content.ReadAsStringAsync();
        Assert.Contains("already closed", recloseErr, StringComparison.OrdinalIgnoreCase);

        // =========================================================================
        // 8. Cannot Delete Drawer with Financial/Shift History
        // =========================================================================
        _output.WriteLine("=== 8. Drawer History Preservation (No Hard Delete) ===");
        var deleteDrawerRes = await managerClient.DeleteAsync($"/api/shifts/drawers/{drawer1.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteDrawerRes.StatusCode);
        var delErr = await deleteDrawerRes.Content.ReadAsStringAsync();
        Assert.Contains("Cannot delete a cash drawer that has existing shift", delErr, StringComparison.OrdinalIgnoreCase);

        // Cannot change branch of drawer with shift history
        var changeBranchRes = await managerClient.PutAsJsonAsync($"/api/shifts/drawers/{drawer1.Id}", new UpdateCashDrawerRequest(
            DrawerCode: drawer1.DrawerCode,
            Name: drawer1.Name,
            BranchId: Guid.NewGuid() // Different branch
        ));
        Assert.Equal(HttpStatusCode.BadRequest, changeBranchRes.StatusCode);
        var chgBranchErr = await changeBranchRes.Content.ReadAsStringAsync();
        Assert.Contains("Cannot change the branch of a cash drawer that has shift history", chgBranchErr, StringComparison.OrdinalIgnoreCase);

        // =========================================================================
        // 9. Next Shift Inherits Previous Actual Closing Balance (Rs. 52,500.00)
        // =========================================================================
        _output.WriteLine("=== 9. Inherit Previous Shift Closing Balance as Opening Float ===");
        var defaultsRes2 = await cashier2Client.GetFromJsonAsync<ShiftOpeningDefaultsDto>(
            $"/api/shifts/opening-defaults/{drawer1.Id}"
        );
        Assert.NotNull(defaultsRes2);
        Assert.Equal(52500.00m, defaultsRes2.SystemExpectedOpeningBalance);
        Assert.Equal(shift1.ShiftNumber, defaultsRes2.PreviousShiftNumber);

        // Cashier 2 opens Shift 2 with exact expected float
        var openRes2 = await cashier2Client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer1.Id,
            OpeningBalance: 52500.00m,
            OpeningDifferenceReason: null,
            OpeningNotes: "Shift handover from Cashier 1"
        ));
        Assert.Equal(HttpStatusCode.Created, openRes2.StatusCode);
        var shift2 = (await openRes2.Content.ReadFromJsonAsync<ShiftDto>())!;

        // Normal Cashier 2 closes their own shift with Shortage Variance
        var closeRes2 = await cashier2Client.PostAsJsonAsync($"/api/shifts/{shift2.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 51000.00m,
            ClosingVarianceReason: "Petty cash voucher #488 paid out for water supply (Rs. 1,500)",
            ClosingNotes: "Voucher attached to physical cash bag",
            RowVersion: shift2.Version
        ));
        Assert.Equal(HttpStatusCode.OK, closeRes2.StatusCode);
        var closedShift2 = (await closeRes2.Content.ReadFromJsonAsync<ShiftDto>())!;

        Assert.False(closedShift2.IsOverrideClose);
        Assert.Null(closedShift2.OverrideReason);
        Assert.Equal(cashier2User.Id, closedShift2.ClosedBy);
        Assert.Equal(-1500.00m, closedShift2.ClosingVariance); // Negative indicates shortage

        _output.WriteLine("=== MODULE 11 INTEGRATION TESTS COMPLETED SUCCESSFULLY ===");
    }

    [Fact]
    public async Task ConcurrentShiftOpening_OnSameDrawer_DatabaseConstraintEnsuresExactlyOneSucceeds()
    {
        var client = _factory.CreateClient();

        // 1. Authenticate as Owner
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            Username: "saranga_owner",
            Password: "OwnerSuperAdmin@2026#Secure"
        ));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var ownerToken = (await loginRes.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // 2. Setup Test Branch, Drawer and 2 Cashiers
        var branchRes = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test M11 Concurrency Branch",
            Code: "T11B1",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        var branch = (await branchRes.Content.ReadFromJsonAsync<BranchDto>())!;

        var drawerRes = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(
            BranchId: branch.Id,
            DrawerCode: "T11DRW9",
            Name: "Contention Drawer",
            Description: "Testing database level active shift uniqueness"
        ));
        var drawer = (await drawerRes.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        var rolesRes = await ownerClient.GetFromJsonAsync<List<Saranga.Application.DTOs.Roles.RoleDto>>("/api/roles");
        var cashierRole = rolesRes!.First(r => r.Name == SystemRoles.Cashier);

        // Cashier A
        await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m11_concurrent_a",
            Email: "test_m11_concurrent_a@saranga.lk",
            Password: "Cashier@2026#Secure",
            FullName: "Concurrent Cashier A",
            RoleIds: new List<Guid> { cashierRole.Id },
            BranchIds: new List<Guid> { branch.Id },
            DefaultBranchId: branch.Id
        ));

        // Cashier B
        await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m11_concurrent_b",
            Email: "test_m11_concurrent_b@saranga.lk",
            Password: "Cashier@2026#Secure",
            FullName: "Concurrent Cashier B",
            RoleIds: new List<Guid> { cashierRole.Id },
            BranchIds: new List<Guid> { branch.Id },
            DefaultBranchId: branch.Id
        ));

        // Authenticate Cashier A
        var loginA = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m11_concurrent_a", "Cashier@2026#Secure"));
        var tokenA = (await loginA.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Authenticate Cashier B
        var loginB = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m11_concurrent_b", "Cashier@2026#Secure"));
        var tokenB = (await loginB.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Prepare identical open requests on the exact same drawer
        var openReqA = new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer.Id,
            OpeningBalance: 10000.00m,
            OpeningDifferenceReason: "Test M11 Race contention test"
        );

        var openReqB = new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer.Id,
            OpeningBalance: 10000.00m,
            OpeningDifferenceReason: "Test M11 Race contention test"
        );

        // 3. Dispatch concurrently via Task.WhenAll
        var taskA = clientA.PostAsJsonAsync("/api/shifts/open", openReqA);
        var taskB = clientB.PostAsJsonAsync("/api/shifts/open", openReqB);

        var responses = await Task.WhenAll(taskA, taskB);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rejectedCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        _output.WriteLine($"Concurrency Results -> Success: {successCount}, Rejected: {rejectedCount}");

        // Exactly one request must succeed, and the competing request must be rejected with 400 Bad Request (never 500)
        Assert.Equal(1, successCount);
        Assert.Equal(1, rejectedCount);

        var failedRes = responses.First(r => r.StatusCode == HttpStatusCode.BadRequest);
        var errBody = await failedRes.Content.ReadAsStringAsync();
        Assert.Contains("active open shift", errBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentShiftClosing_AgainstSameShift_OnlyOneSucceeds()
    {
        var client = _factory.CreateClient();

        // 1. Authenticate as Owner
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            Username: "saranga_owner",
            Password: "OwnerSuperAdmin@2026#Secure"
        ));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var ownerToken = (await loginRes.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // 2. Setup Branch, Drawer and Cashier
        var branchRes = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test M11 Close Concurrency Branch",
            Code: "T11B1",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        var branch = (await branchRes.Content.ReadFromJsonAsync<BranchDto>())!;

        var drawerRes = await ownerClient.PostAsJsonAsync("/api/shifts/drawers", new CreateCashDrawerRequest(
            BranchId: branch.Id,
            DrawerCode: "T11DRW7",
            Name: "Close Race Drawer"
        ));
        var drawer = (await drawerRes.Content.ReadFromJsonAsync<CashDrawerDto>())!;

        var rolesRes = await ownerClient.GetFromJsonAsync<List<Saranga.Application.DTOs.Roles.RoleDto>>("/api/roles");
        var cashierRole = rolesRes!.First(r => r.Name == SystemRoles.Cashier);

        await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m11_close_race",
            Email: "test_m11_close_race@saranga.lk",
            Password: "Cashier@2026#Secure",
            FullName: "Close Race Cashier",
            RoleIds: new List<Guid> { cashierRole.Id },
            BranchIds: new List<Guid> { branch.Id },
            DefaultBranchId: branch.Id
        ));

        var cashierLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("test_m11_close_race", "Cashier@2026#Secure"));
        var cashierToken = (await cashierLogin.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var cashierClient1 = _factory.CreateClient();
        cashierClient1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);

        var cashierClient2 = _factory.CreateClient();
        cashierClient2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);

        // 3. Open a shift
        var openRes = await cashierClient1.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer.Id,
            OpeningBalance: 20000.00m,
            OpeningDifferenceReason: "Test M11 Close race initial float"
        ));
        Assert.Equal(HttpStatusCode.Created, openRes.StatusCode);
        var shift = (await openRes.Content.ReadFromJsonAsync<ShiftDto>())!;

        // 4. Dispatch two concurrent close requests against the same shift using the same initial RowVersion
        var closeReq1 = new CloseShiftRequest(
            ActualClosingBalance: 20000.00m,
            ClosingVarianceReason: null,
            RowVersion: shift.Version
        );

        var closeReq2 = new CloseShiftRequest(
            ActualClosingBalance: 20000.00m,
            ClosingVarianceReason: null,
            RowVersion: shift.Version
        );

        var task1 = cashierClient1.PostAsJsonAsync($"/api/shifts/{shift.Id}/close", closeReq1);
        var task2 = cashierClient2.PostAsJsonAsync($"/api/shifts/{shift.Id}/close", closeReq2);

        var responses = await Task.WhenAll(task1, task2);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejectedCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest || r.StatusCode == HttpStatusCode.Conflict);

        _output.WriteLine($"Close Concurrency Results -> Success: {successCount}, Rejected: {rejectedCount}");

        // Exactly one closing operation must succeed
        Assert.Equal(1, successCount);
        Assert.Equal(1, rejectedCount);

        // Second request receives a controlled business rule / conflict error, never 500
        var failedRes = responses.First(r => r.StatusCode != HttpStatusCode.OK);
        var errBody = await failedRes.Content.ReadAsStringAsync();
        Assert.True(
            errBody.Contains("already closed", StringComparison.OrdinalIgnoreCase) ||
            errBody.Contains("modified concurrently", StringComparison.OrdinalIgnoreCase),
            $"Expected clean rejection message but got: {errBody}"
        );
    }
}

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
        // 2. Setup Test Branch, Drawers, and Cashiers
        // =========================================================================
        _output.WriteLine("=== 2. Setup Test Branch, Cash Drawers, and Cashiers ===");
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

        // Register 2 Cash Drawers
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

        // Get Cashier Role ID
        var rolesRes = await ownerClient.GetFromJsonAsync<List<Saranga.Application.DTOs.Roles.RoleDto>>("/api/roles");
        Assert.NotNull(rolesRes);
        var cashierRole = rolesRes.First(r => r.Name == SystemRoles.Cashier);

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

        // =========================================================================
        // 3. First Shift: Defaults to Rs. 0.00 Expected Float
        // =========================================================================
        _output.WriteLine("=== 3. First Shift Opening Float Defaults ===");
        var defaultsRes1 = await cashier1Client.GetFromJsonAsync<ShiftOpeningDefaultsDto>(
            $"/api/shifts/opening-defaults/{drawer1.Id}"
        );
        Assert.NotNull(defaultsRes1);
        Assert.Equal(0.00m, defaultsRes1.SystemExpectedOpeningBalance);
        Assert.Null(defaultsRes1.PreviousShiftNumber);

        // Opening with Rs. 50,000 float when system expects 0 requires a reason!
        var openWithoutReasonRes = await cashier1Client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer1.Id,
            OpeningBalance: 50000.00m,
            OpeningDifferenceReason: null // Missing mandatory reason!
        ));
        Assert.Equal(HttpStatusCode.BadRequest, openWithoutReasonRes.StatusCode);

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
        Assert.Equal(50000.00m, shift1.OpeningBalance);
        Assert.Equal(0.00m, shift1.SystemExpectedOpeningBalance);
        Assert.Equal(50000.00m, shift1.OpeningBalanceDifference);
        Assert.Equal(cashier1User.Id, shift1.CashierId);
        _output.WriteLine($"Shift 1 Opened: {shift1.ShiftNumber}");

        // =========================================================================
        // 4. Invariant: One Active Shift Per Cash Drawer
        // =========================================================================
        _output.WriteLine("=== 4. One Active Shift Per Cash Drawer ===");
        // Cashier 2 tries to open a shift on Drawer 1 while Shift 1 is still open
        var openConflictDrawerRes = await cashier2Client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer1.Id, // Same drawer!
            OpeningBalance: 50000.00m,
            OpeningDifferenceReason: "Conflict test"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, openConflictDrawerRes.StatusCode);
        var errDrawer = await openConflictDrawerRes.Content.ReadAsStringAsync();
        Assert.Contains("already has an active open shift", errDrawer, StringComparison.OrdinalIgnoreCase);

        // =========================================================================
        // 5. Invariant: One Active Shift Per Cashier (MVP)
        // =========================================================================
        _output.WriteLine("=== 5. One Active Shift Per Cashier ===");
        // Cashier 1 tries to open a second shift on Drawer 2 while Shift 1 is still open
        var openConflictCashierRes = await cashier1Client.PostAsJsonAsync("/api/shifts/open", new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer2.Id, // Different drawer
            OpeningBalance: 25000.00m,
            OpeningDifferenceReason: "Cashier conflict test"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, openConflictCashierRes.StatusCode);
        var errCashier = await openConflictCashierRes.Content.ReadAsStringAsync();
        Assert.Contains("Cashier already has an active open shift", errCashier, StringComparison.OrdinalIgnoreCase);

        // =========================================================================
        // 6. Close Shift 1: Backend Calculation, Variance, & Mandatory Reason
        // =========================================================================
        _output.WriteLine("=== 6. Close Shift 1 & Reconcile Variance ===");
        // Actual physical cash counted: Rs. 52,500.00
        // Backend Expected Closing: Rs. 50,000.00
        // Variance: +Rs. 2,500.00 (Surplus)
        // Attempting to close with variance without reason must fail
        var closeWithoutReasonRes = await cashier1Client.PostAsJsonAsync($"/api/shifts/{shift1.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 52500.00m,
            ClosingVarianceReason: null, // Missing mandatory reason!
            RowVersion: shift1.Version
        ));
        Assert.Equal(HttpStatusCode.BadRequest, closeWithoutReasonRes.StatusCode);

        // Close with mandatory explanation reason
        var closeRes1 = await cashier1Client.PostAsJsonAsync($"/api/shifts/{shift1.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 52500.00m,
            ClosingVarianceReason: "Customer cash tip/surplus rounded into drawer",
            ClosingNotes: "Safe deposit bag sealed",
            RowVersion: shift1.Version
        ));
        Assert.Equal(HttpStatusCode.OK, closeRes1.StatusCode);
        var closedShift1 = (await closeRes1.Content.ReadFromJsonAsync<ShiftDto>())!;

        Assert.Equal(ShiftStatus.Closed, closedShift1.Status);
        Assert.NotNull(closedShift1.ClosedAt);
        Assert.Equal(50000.00m, closedShift1.ExpectedClosingBalance);
        Assert.Equal(52500.00m, closedShift1.ActualClosingBalance);
        Assert.Equal(2500.00m, closedShift1.ClosingVariance);
        Assert.Equal("Customer cash tip/surplus rounded into drawer", closedShift1.ClosingVarianceReason);

        // =========================================================================
        // 7. Immutability: Closed Shifts Cannot Be Re-Closed or Modified
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
        // 8. Next Shift Inherits Previous Actual Closing Balance (Rs. 52,500.00)
        // =========================================================================
        _output.WriteLine("=== 8. Inherit Previous Shift Closing Balance as Opening Float ===");
        var defaultsRes2 = await cashier2Client.GetFromJsonAsync<ShiftOpeningDefaultsDto>(
            $"/api/shifts/opening-defaults/{drawer1.Id}"
        );
        Assert.NotNull(defaultsRes2);
        // Must equal Shift 1's actual closing cash
        Assert.Equal(52500.00m, defaultsRes2.SystemExpectedOpeningBalance);
        Assert.Equal(shift1.ShiftNumber, defaultsRes2.PreviousShiftNumber);

        // Cashier 2 opens Shift 2 on Drawer 1 with exact expected float (Rs. 52,500.00) -> 0 difference -> No reason needed!
        var openReq2 = new OpenShiftRequest(
            BranchId: branch.Id,
            CashDrawerId: drawer1.Id,
            OpeningBalance: 52500.00m,
            OpeningDifferenceReason: null, // No reason needed because diff is 0.00
            OpeningNotes: "Shift handover from Cashier 1"
        );
        var openRes2 = await cashier2Client.PostAsJsonAsync("/api/shifts/open", openReq2);
        Assert.Equal(HttpStatusCode.Created, openRes2.StatusCode);
        var shift2 = (await openRes2.Content.ReadFromJsonAsync<ShiftDto>())!;

        Assert.Equal(ShiftStatus.Open, shift2.Status);
        Assert.Equal(52500.00m, shift2.OpeningBalance);
        Assert.Equal(52500.00m, shift2.SystemExpectedOpeningBalance);
        Assert.Equal(0.00m, shift2.OpeningBalanceDifference);
        Assert.Null(shift2.OpeningDifferenceReason);
        Assert.NotEqual(shift1.ShiftNumber, shift2.ShiftNumber);

        // =========================================================================
        // 9. Close Shift 2 with Shortage Variance (Rs. 51,000 actual vs 52,500 expected)
        // =========================================================================
        _output.WriteLine("=== 9. Close Shift 2 with Cash Shortage Variance ===");
        var closeRes2 = await cashier2Client.PostAsJsonAsync($"/api/shifts/{shift2.Id}/close", new CloseShiftRequest(
            ActualClosingBalance: 51000.00m,
            ClosingVarianceReason: "Petty cash voucher #488 paid out for water supply (Rs. 1,500)",
            ClosingNotes: "Voucher attached to physical cash bag",
            RowVersion: shift2.Version
        ));
        Assert.Equal(HttpStatusCode.OK, closeRes2.StatusCode);
        var closedShift2 = (await closeRes2.Content.ReadFromJsonAsync<ShiftDto>())!;

        Assert.Equal(ShiftStatus.Closed, closedShift2.Status);
        Assert.Equal(52500.00m, closedShift2.ExpectedClosingBalance);
        Assert.Equal(51000.00m, closedShift2.ActualClosingBalance);
        Assert.Equal(-1500.00m, closedShift2.ClosingVariance); // Negative indicates cash shortage

        // =========================================================================
        // 10. Query Shift History & Active Cashier Shift Retrieval
        // =========================================================================
        _output.WriteLine("=== 10. Query Shift History & Board ===");
        var historyRes = await cashier1Client.GetFromJsonAsync<PagedResult<ShiftDto>>(
            $"/api/shifts?branchId={branch.Id}"
        );
        Assert.NotNull(historyRes);
        Assert.True(historyRes.TotalCount >= 2);
        Assert.Contains(historyRes.Items, s => s.Id == shift1.Id);
        Assert.Contains(historyRes.Items, s => s.Id == shift2.Id);

        // Verify active cashier returns 204 No Content when no shift is currently open
        var noActiveRes = await cashier1Client.GetAsync("/api/shifts/active-cashier");
        Assert.Equal(HttpStatusCode.NoContent, noActiveRes.StatusCode);

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
}

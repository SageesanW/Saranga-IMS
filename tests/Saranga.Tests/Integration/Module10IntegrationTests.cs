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
using Saranga.Application.DTOs.Customers;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module10IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Module10IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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
            DELETE FROM ""Customers"" WHERE ""FullName"" LIKE 'Test M10 %' OR ""PrimaryPhone"" LIKE '077999%' OR ""PrimaryPhone"" LIKE '077 999%' OR ""NormalizedPrimaryPhone"" LIKE '+9477999%';
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" = 'T10B1')
                OR ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m10_%');
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m10_%');
            DELETE FROM ""Users"" WHERE ""Username"" LIKE 'test_m10_%';
            DELETE FROM ""Branches"" WHERE ""Code"" = 'T10B1';
        ");
    }

    [Fact]
    public async Task ComprehensiveModule10_CustomerManagement_EndToEndVerification()
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
        // 2. Setup Cashier User to verify role permissions
        // =========================================================================
        _output.WriteLine("=== 2. Setup Test Branch and Cashier User ===");
        var branchRes = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test M10 Flagship Branch",
            Code: "T10B1",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Created, branchRes.StatusCode);
        var branch = (await branchRes.Content.ReadFromJsonAsync<BranchDto>())!;

        // Get Cashier Role ID
        var rolesRes = await ownerClient.GetFromJsonAsync<List<Saranga.Application.DTOs.Roles.RoleDto>>("/api/roles");
        Assert.NotNull(rolesRes);
        var cashierRole = rolesRes.First(r => r.Name == SystemRoles.Cashier);

        // Create Cashier User
        var cashierUserRes = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: "test_m10_cashier",
            Email: "test_m10_cashier@saranga.lk",
            Password: "Cashier@2026#Secure",
            FullName: "M10 Cashier",
            RoleIds: new List<Guid> { cashierRole.Id },
            BranchIds: new List<Guid> { branch.Id },
            DefaultBranchId: branch.Id
        ));
        Assert.Equal(HttpStatusCode.Created, cashierUserRes.StatusCode);

        // Login as Cashier
        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            Username: "test_m10_cashier",
            Password: "Cashier@2026#Secure"
        ));
        Assert.Equal(HttpStatusCode.OK, cashierLoginRes.StatusCode);
        var cashierLoginData = await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(cashierLoginData);
        var cashierToken = cashierLoginData.AccessToken;

        var cashierClient = _factory.CreateClient();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);

        // =========================================================================
        // 3. Create Customer with Auto-Generated Sequence CustomerCode
        // =========================================================================
        _output.WriteLine("=== 3. Create Customer with Auto-Generated CustomerCode ===");
        var createCustomerReq1 = new CreateCustomerRequest(
            FullName: "Test M10 Perera Kamal",
            PrimaryPhone: "077 999 1111",
            SecondaryPhone: "011 234 5678",
            Email: "perera.kamal@example.lk",
            AddressLine1: "123 Galle Road",
            AddressLine2: "Suite 4B",
            City: "Colombo",
            DistrictProvince: "Western Province",
            PostalCode: "00300",
            Country: "Sri Lanka",
            DateOfBirth: new DateOnly(1988, 5, 12),
            Notes: "VIP customer - prefers 22K yellow gold bangles"
        );

        var createRes1 = await cashierClient.PostAsJsonAsync("/api/customers", createCustomerReq1);
        Assert.Equal(HttpStatusCode.Created, createRes1.StatusCode);
        var customer1 = (await createRes1.Content.ReadFromJsonAsync<CustomerDto>())!;

        Assert.NotNull(customer1);
        Assert.NotEqual(Guid.Empty, customer1.Id);
        Assert.StartsWith("CUS-", customer1.CustomerCode);
        Assert.Equal("Test M10 Perera Kamal", customer1.FullName);
        Assert.Equal("077 999 1111", customer1.PrimaryPhone);
        Assert.Equal("+94779991111", customer1.NormalizedPrimaryPhone);
        Assert.Equal("+94112345678", customer1.NormalizedSecondaryPhone);
        Assert.Equal("perera.kamal@example.lk", customer1.Email);
        Assert.True(customer1.IsActive);
        Assert.True(customer1.Version > 0);

        _output.WriteLine($"Generated Customer 1 Code: {customer1.CustomerCode}");

        // =========================================================================
        // 4. Verify Second Customer Gets Consecutive Auto-Sequence CustomerCode
        // =========================================================================
        _output.WriteLine("=== 4. Consecutive Sequence Code for Second Customer ===");
        var createCustomerReq2 = new CreateCustomerRequest(
            FullName: "Test M10 Sunethra Silva",
            PrimaryPhone: "+94 77 999 2222",
            SecondaryPhone: null,
            Email: "sunethra.silva@example.lk",
            AddressLine1: "45 Main Street",
            AddressLine2: null,
            City: "Jaffna",
            DistrictProvince: "Northern Province",
            PostalCode: "40000",
            Country: "Sri Lanka",
            DateOfBirth: null,
            Notes: null
        );

        var createRes2 = await cashierClient.PostAsJsonAsync("/api/customers", createCustomerReq2);
        Assert.Equal(HttpStatusCode.Created, createRes2.StatusCode);
        var customer2 = (await createRes2.Content.ReadFromJsonAsync<CustomerDto>())!;

        Assert.StartsWith("CUS-", customer2.CustomerCode);
        Assert.NotEqual(customer1.CustomerCode, customer2.CustomerCode);
        Assert.Equal("+94779992222", customer2.NormalizedPrimaryPhone);

        // =========================================================================
        // 5. Phone Normalization Invariants (International & Non-+94 preservation)
        // =========================================================================
        _output.WriteLine("=== 5. International Phone Number Preservation ===");
        var createCustomerReq3 = new CreateCustomerRequest(
            FullName: "Test M10 Diaspora Customer",
            PrimaryPhone: "+1 (416) 999-3333", // Canada / US international phone
            SecondaryPhone: null,
            Email: null,
            AddressLine1: "100 Bay Street",
            AddressLine2: null,
            City: "Toronto",
            DistrictProvince: "Ontario",
            PostalCode: "M5J 2T3",
            Country: "Canada",
            DateOfBirth: null,
            Notes: "Visiting Colombo annually for festive jewellery purchases"
        );

        var createRes3 = await cashierClient.PostAsJsonAsync("/api/customers", createCustomerReq3);
        Assert.Equal(HttpStatusCode.Created, createRes3.StatusCode);
        var customer3 = (await createRes3.Content.ReadFromJsonAsync<CustomerDto>())!;

        // Must preserve +1 country code without forcing +94
        Assert.Equal("+14169993333", customer3.NormalizedPrimaryPhone);
        Assert.Equal("Test M10 Diaspora Customer", customer3.FullName);

        // Invalid short phone (< 7 digits) rejected with 400
        var invalidPhoneReq = new CreateCustomerRequest(
            FullName: "Test M10 Invalid Phone",
            PrimaryPhone: "12345",
            SecondaryPhone: null,
            Email: null,
            AddressLine1: null,
            AddressLine2: null,
            City: null,
            DistrictProvince: null,
            PostalCode: null,
            Country: null,
            DateOfBirth: null,
            Notes: null
        );
        var invalidPhoneRes = await cashierClient.PostAsJsonAsync("/api/customers", invalidPhoneReq);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPhoneRes.StatusCode);

        // =========================================================================
        // 6. Duplicate Detection Invariants (Check endpoint & Create block)
        // =========================================================================
        _output.WriteLine("=== 6. Duplicate Detection Invariants ===");
        // Check duplicate by formatted phone
        var dupPhoneCheckRes = await cashierClient.GetFromJsonAsync<CustomerDuplicateCheckResult>(
            $"/api/customers/check-duplicate?primaryPhone={Uri.EscapeDataString("077-999-1111")}"
        );
        Assert.NotNull(dupPhoneCheckRes);
        Assert.True(dupPhoneCheckRes.IsDuplicate);
        Assert.NotNull(dupPhoneCheckRes.ExistingCustomer);
        Assert.Equal(customer1.Id, dupPhoneCheckRes.ExistingCustomer.Id);
        Assert.Contains(customer1.CustomerCode, dupPhoneCheckRes.DuplicateReason);
        Assert.Equal("077 999 1111", dupPhoneCheckRes.ExistingCustomer.PrimaryPhone);

        // Check duplicate by email
        var dupEmailCheckRes = await cashierClient.GetFromJsonAsync<CustomerDuplicateCheckResult>(
            $"/api/customers/check-duplicate?primaryPhone=0779999999&email={Uri.EscapeDataString("PERERA.KAMAL@EXAMPLE.LK")}"
        );
        Assert.NotNull(dupEmailCheckRes);
        Assert.True(dupEmailCheckRes.IsDuplicate);
        Assert.NotNull(dupEmailCheckRes.ExistingCustomer);
        Assert.Equal(customer1.Id, dupEmailCheckRes.ExistingCustomer.Id);

        // Check non-duplicate returns false
        var cleanCheckRes = await cashierClient.GetFromJsonAsync<CustomerDuplicateCheckResult>(
            "/api/customers/check-duplicate?primaryPhone=0779998888&email=brandnew@example.com"
        );
        Assert.NotNull(cleanCheckRes);
        Assert.False(cleanCheckRes.IsDuplicate);

        // Attempting to create duplicate customer with same phone must be rejected (HTTP 400)
        var duplicateCreateReq = new CreateCustomerRequest(
            FullName: "Test M10 Duplicate Impostor",
            PrimaryPhone: "077 999 1111", // Exactly same normalized phone as Customer 1
            SecondaryPhone: null,
            Email: "different.email@example.com",
            AddressLine1: null,
            AddressLine2: null,
            City: null,
            DistrictProvince: null,
            PostalCode: null,
            Country: null,
            DateOfBirth: null,
            Notes: null
        );
        var duplicateCreateRes = await cashierClient.PostAsJsonAsync("/api/customers", duplicateCreateReq);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateCreateRes.StatusCode);

        // =========================================================================
        // 7. Update Customer & Optimistic Concurrency Protection (RowVersion / xmin)
        // =========================================================================
        _output.WriteLine("=== 7. Update Customer & Optimistic Concurrency ===");
        uint staleRowVersion = 999999;
        var updateConflictReq = new UpdateCustomerRequest(
            FullName: "Test M10 Perera Kamal Updated",
            PrimaryPhone: "077 999 1111",
            SecondaryPhone: "011 234 9999",
            Email: "perera.kamal.new@example.lk",
            AddressLine1: "123 Galle Road, 5th Floor",
            AddressLine2: null,
            City: "Colombo",
            DistrictProvince: "Western Province",
            PostalCode: "00300",
            Country: "Sri Lanka",
            DateOfBirth: new DateOnly(1988, 5, 12),
            Notes: "Updated VIP preferences",
            RowVersion: staleRowVersion // Mismatched concurrency token
        );

        var conflictRes = await cashierClient.PutAsJsonAsync($"/api/customers/{customer1.Id}", updateConflictReq);
        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);

        // Now update with correct valid RowVersion
        var validUpdateReq = updateConflictReq with { RowVersion = customer1.Version };
        var validUpdateRes = await cashierClient.PutAsJsonAsync($"/api/customers/{customer1.Id}", validUpdateReq);
        Assert.Equal(HttpStatusCode.OK, validUpdateRes.StatusCode);
        var updatedCustomer1 = (await validUpdateRes.Content.ReadFromJsonAsync<CustomerDto>())!;

        Assert.Equal("Test M10 Perera Kamal Updated", updatedCustomer1.FullName);
        Assert.Equal("+94112349999", updatedCustomer1.NormalizedSecondaryPhone);
        Assert.Equal("perera.kamal.new@example.lk", updatedCustomer1.Email);
        Assert.NotEqual(customer1.Version, updatedCustomer1.Version);

        // =========================================================================
        // 8. Soft Activation / Deactivation (IsActive toggle)
        // =========================================================================
        _output.WriteLine("=== 8. Soft Activation / Deactivation ===");
        // Cashier has CustomerManage which allows status update
        var deactivateReq = new UpdateCustomerStatusRequest(
            IsActive: false,
            Reason: "Customer requested account pause"
        );
        var deactivateRes = await cashierClient.PatchAsJsonAsync($"/api/customers/{customer2.Id}/status", deactivateReq);
        Assert.Equal(HttpStatusCode.OK, deactivateRes.StatusCode);
        var deactivatedCustomer2 = (await deactivateRes.Content.ReadFromJsonAsync<CustomerDto>())!;
        Assert.False(deactivatedCustomer2.IsActive);

        // Query active only -> customer2 must NOT appear
        var activeOnlyRes = await cashierClient.GetFromJsonAsync<PagedResult<CustomerDto>>(
            "/api/customers?isActive=true&search=Sunethra"
        );
        Assert.NotNull(activeOnlyRes);
        Assert.Empty(activeOnlyRes.Items);

        // Query all -> customer2 must appear with IsActive=false (permanent historical record)
        var allQueryRes = await cashierClient.GetFromJsonAsync<PagedResult<CustomerDto>>(
            "/api/customers?search=Sunethra"
        );
        Assert.NotNull(allQueryRes);
        Assert.Single(allQueryRes.Items);
        Assert.False(allQueryRes.Items[0].IsActive);

        // Reactivate customer2
        var reactivateReq = new UpdateCustomerStatusRequest(IsActive: true, Reason: "Reactivated by customer request");
        var reactivateRes = await cashierClient.PatchAsJsonAsync($"/api/customers/{customer2.Id}/status", reactivateReq);
        Assert.Equal(HttpStatusCode.OK, reactivateRes.StatusCode);
        var reactivatedCustomer2 = (await reactivateRes.Content.ReadFromJsonAsync<CustomerDto>())!;
        Assert.True(reactivatedCustomer2.IsActive);

        // =========================================================================
        // 9. Fast POS Lookup / Search Endpoint (/api/customers/search?q=...)
        // =========================================================================
        _output.WriteLine("=== 9. Fast POS Lookup Endpoint ===");
        // Search by Code
        var lookupByCode = await cashierClient.GetFromJsonAsync<List<CustomerLookupDto>>(
            $"/api/customers/search?q={customer1.CustomerCode}"
        );
        Assert.NotNull(lookupByCode);
        Assert.Contains(lookupByCode, c => c.Id == customer1.Id);

        // Search by Phone substring
        var lookupByPhone = await cashierClient.GetFromJsonAsync<List<CustomerLookupDto>>(
            "/api/customers/search?q=9991111"
        );
        Assert.NotNull(lookupByPhone);
        Assert.Contains(lookupByPhone, c => c.Id == customer1.Id);

        // Search by Name substring
        var lookupByName = await cashierClient.GetFromJsonAsync<List<CustomerLookupDto>>(
            "/api/customers/search?q=Sunethra"
        );
        Assert.NotNull(lookupByName);
        Assert.Contains(lookupByName, c => c.Id == customer2.Id);

        // =========================================================================
        // 10. Direct Customer Retrieval by ID and Code
        // =========================================================================
        _output.WriteLine("=== 10. Direct Retrieval by ID and CustomerCode ===");
        var getByIdRes = await cashierClient.GetAsync($"/api/customers/{customer1.Id}");
        Assert.Equal(HttpStatusCode.OK, getByIdRes.StatusCode);

        var getByCodeRes = await cashierClient.GetAsync($"/api/customers/code/{customer1.CustomerCode}");
        Assert.Equal(HttpStatusCode.OK, getByCodeRes.StatusCode);

        var getByNonExistentRes = await cashierClient.GetAsync($"/api/customers/code/CUS-999999");
        Assert.Equal(HttpStatusCode.NotFound, getByNonExistentRes.StatusCode);

        _output.WriteLine("=== MODULE 10 INTEGRATION TESTS COMPLETED SUCCESSFULLY ===");
    }

    [Fact]
    public void CanonicalPhoneNormalization_ResolvesLocalAndInternationalEquivalents()
    {
        // 1. Equivalent Sri Lankan local formats resolve to exact same E.164
        var raw1 = "077 999 1111";
        var raw2 = "+94 77 999 1111";
        var raw3 = "0779991111";
        var raw4 = "+94779991111";
        var raw5 = "077-999-1111";

        var norm1 = Saranga.Domain.Common.PhoneNormalizer.Normalize(raw1);
        var norm2 = Saranga.Domain.Common.PhoneNormalizer.Normalize(raw2);
        var norm3 = Saranga.Domain.Common.PhoneNormalizer.Normalize(raw3);
        var norm4 = Saranga.Domain.Common.PhoneNormalizer.Normalize(raw4);
        var norm5 = Saranga.Domain.Common.PhoneNormalizer.Normalize(raw5);

        Assert.Equal("+94779991111", norm1);
        Assert.Equal("+94779991111", norm2);
        Assert.Equal("+94779991111", norm3);
        Assert.Equal("+94779991111", norm4);
        Assert.Equal("+94779991111", norm5);

        // 2. Landline local formats
        var landline1 = "011 234 5678";
        var landline2 = "+94 11 234 5678";
        Assert.Equal("+94112345678", Saranga.Domain.Common.PhoneNormalizer.Normalize(landline1));
        Assert.Equal("+94112345678", Saranga.Domain.Common.PhoneNormalizer.Normalize(landline2));

        // 3. International formats must preserve foreign country codes and NEVER be forced to +94
        var canadaUS = "+1 (416) 999-3333";
        var uk = "+44 20 7946 0991";
        var australia = "+61 491 570 156";
        var uae = "+971 50 123 4567";

        Assert.Equal("+14169993333", Saranga.Domain.Common.PhoneNormalizer.Normalize(canadaUS));
        Assert.Equal("+442079460991", Saranga.Domain.Common.PhoneNormalizer.Normalize(uk));
        Assert.Equal("+61491570156", Saranga.Domain.Common.PhoneNormalizer.Normalize(australia));
        Assert.Equal("+971501234567", Saranga.Domain.Common.PhoneNormalizer.Normalize(uae));

        // 4. Null and whitespace
        Assert.Null(Saranga.Domain.Common.PhoneNormalizer.Normalize(null));
        Assert.Null(Saranga.Domain.Common.PhoneNormalizer.Normalize("   "));
    }

    [Fact]
    public async Task ConcurrentCustomerCreation_WithSameNormalizedPhone_DatabaseConstraintEnsuresExactlyOneSucceeds()
    {
        var client = _factory.CreateClient();

        // Login as Owner
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            Username: "saranga_owner",
            Password: "OwnerSuperAdmin@2026#Secure"
        ));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginData = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginData);

        var authClient1 = _factory.CreateClient();
        authClient1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginData.AccessToken);

        var authClient2 = _factory.CreateClient();
        authClient2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginData.AccessToken);

        // Prepare 2 creation requests with differing formatted strings that resolve to the same E.164 phone: +94779997777
        var req1 = new CreateCustomerRequest(
            FullName: "Test M10 Concurrent Customer A",
            PrimaryPhone: "077 999 7777",
            SecondaryPhone: null,
            Email: "concurrent.a@saranga.lk"
        );

        var req2 = new CreateCustomerRequest(
            FullName: "Test M10 Concurrent Customer B",
            PrimaryPhone: "+94 77 999 7777", // Equivalent normalized phone
            SecondaryPhone: null,
            Email: "concurrent.b@saranga.lk"
        );

        // Dispatch simultaneously to test database unique constraint protection against race conditions
        var task1 = authClient1.PostAsJsonAsync("/api/customers", req1);
        var task2 = authClient2.PostAsJsonAsync("/api/customers", req2);

        var responses = await Task.WhenAll(task1, task2);

        var res1 = responses[0];
        var res2 = responses[1];

        _output.WriteLine($"Concurrent Response 1: {res1.StatusCode}");
        _output.WriteLine($"Concurrent Response 2: {res2.StatusCode}");

        // Exactly one request must succeed with 201 Created
        // The other must be rejected with 400 Bad Request (BusinessRuleException from unique constraint), NEVER 500!
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var duplicateCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        Assert.Equal(1, successCount);
        Assert.Equal(1, duplicateCount);

        // Verify the failed response does not expose a 500 error and has a clean message
        var failedRes = responses.First(r => r.StatusCode == HttpStatusCode.BadRequest);
        var errBody = await failedRes.Content.ReadAsStringAsync();
        Assert.Contains("already exists", errBody, StringComparison.OrdinalIgnoreCase);
    }
}

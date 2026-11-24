using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Api;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Attributes;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.DTOs.Branches;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Rates;
using Saranga.Application.DTOs.Roles;
using Saranga.Application.DTOs.Users;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class FakeMetalRateProvider : IMetalRateProvider
{
    public string ProviderName => "TestFakeGoldApi";
    public bool ShouldFail { get; set; } = false;
    public decimal Rate24K { get; set; } = 30000.0000m;
    public Dictionary<decimal, decimal> KaratRates { get; set; } = new()
    {
        [24.0m] = 30000.0000m,
        [22.0m] = 27500.0000m,
        [18.0m] = 22500.0000m
    };

    public Task<MetalRateProviderResult> FetchRatesAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            return Task.FromResult(new MetalRateProviderResult(
                IsSuccess: false,
                ProviderName: ProviderName,
                Timestamp: DateTimeOffset.UtcNow,
                NormalizedRates: new List<ProviderNormalizedRate>(),
                ErrorMessage: "Simulated upstream provider outage / network timeout"
            ));
        }

        var normalized = new ProviderNormalizedRate("XAU", Rate24K, KaratRates);
        return Task.FromResult(new MetalRateProviderResult(
            IsSuccess: true,
            ProviderName: ProviderName,
            Timestamp: DateTimeOffset.UtcNow,
            NormalizedRates: new List<ProviderNormalizedRate> { normalized }
        ));
    }
}

public class Module9IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;
    private static readonly FakeMetalRateProvider FakeProvider = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Module9IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace live HTTP provider with deterministic fake provider for test isolation
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMetalRateProvider));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IMetalRateProvider>(FakeProvider);
            });
        });
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
            DELETE FROM ""MetalRates"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T9B1', 'T9B2'));
            DELETE FROM ""VatConfigurations"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T9B1', 'T9B2'));

            DELETE FROM ""Materials"" WHERE ""Code"" LIKE 'TEST_M9_%';
            DELETE FROM ""Purities"" WHERE ""Code"" LIKE 'TEST_M9_%';

            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" IN ('T9B1', 'T9B2'))
                OR ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m9_%');
            DELETE FROM ""UserRoles"" WHERE ""UserId"" IN (SELECT ""Id"" FROM ""Users"" WHERE ""Username"" LIKE 'test_m9_%');
            DELETE FROM ""Users"" WHERE ""Username"" LIKE 'test_m9_%';
            DELETE FROM ""Branches"" WHERE ""Code"" IN ('T9B1', 'T9B2');
        ");
    }

    [Fact]
    public async Task ComprehensiveModule9_GoldRates_And_VatConfiguration_EndToEndVerification()
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
        // 2. Setup Test Branches & Attributes
        // =========================================================================
        _output.WriteLine("=== 2. Setup Test Branches & Masters ===");
        var b1Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test M9 Colombo Flagship",
            Code: "T9B1",
            Country: "LK",
            CurrencyCode: "LKR",
            TimeZone: "Asia/Colombo",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Created, b1Res.StatusCode);
        var b1 = (await b1Res.Content.ReadFromJsonAsync<BranchDto>())!;

        var b2Res = await ownerClient.PostAsJsonAsync("/api/branches", new CreateBranchRequest(
            Name: "Test M9 Toronto Branch",
            Code: "T9B2",
            Country: "CA",
            CurrencyCode: "CAD",
            TimeZone: "America/Toronto",
            IsActive: true
        ));
        Assert.Equal(HttpStatusCode.Created, b2Res.StatusCode);
        var b2 = (await b2Res.Content.ReadFromJsonAsync<BranchDto>())!;

        var matGoldRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Test M9 Gold", "TEST_M9_GOLD", "Pure yellow gold", true, 1));
        var matGold = (await matGoldRes.Content.ReadFromJsonAsync<MaterialDto>())!;

        var matSilverRes = await ownerClient.PostAsJsonAsync("/api/materials", new CreateMaterialRequest("Test M9 Silver", "TEST_M9_SLV", "Sterling silver", true, 2));
        var matSilver = (await matSilverRes.Content.ReadFromJsonAsync<MaterialDto>())!;

        var pur24kRes = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("Test M9 24K", "TEST_M9_24K", 24.0m, 100.0m, "Pure 24K gold", true, 1));
        var pur24k = (await pur24kRes.Content.ReadFromJsonAsync<PurityDto>())!;

        var pur22kRes = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("Test M9 22K", "TEST_M9_22K", 22.0m, 91.67m, "Standard 22K jewellery", true, 2));
        var pur22k = (await pur22kRes.Content.ReadFromJsonAsync<PurityDto>())!;

        var pur18kRes = await ownerClient.PostAsJsonAsync("/api/purities", new CreatePurityRequest("Test M9 18K", "TEST_M9_18K", 18.0m, 75.0m, "18K white/yellow", true, 3));
        var pur18k = (await pur18kRes.Content.ReadFromJsonAsync<PurityDto>())!;

        // =========================================================================
        // 3. Manual Rate Creation & numeric(18,4) Precision
        // =========================================================================
        _output.WriteLine("=== 3. Manual Rate Creation with 18,4 Precision ===");
        var manualRateReq = new CreateManualRateRequest(
            BranchId: b1.Id,
            MaterialId: matGold.Id,
            PurityId: pur22k.Id,
            RatePerGram: 28500.5555m,
            CurrencyCode: "LKR",
            EffectiveFrom: DateTimeOffset.UtcNow.AddMinutes(-30),
            Notes: "Morning manual board update"
        );
        var createManualRes = await ownerClient.PostAsJsonAsync("/api/metal-rates/manual", manualRateReq);
        var resStr = await createManualRes.Content.ReadAsStringAsync();
        _output.WriteLine($"Manual Rate Response ({createManualRes.StatusCode}): {resStr}");
        Assert.Equal(HttpStatusCode.Created, createManualRes.StatusCode);
        var rate1 = (await createManualRes.Content.ReadFromJsonAsync<MetalRateDto>())!;

        Assert.Equal(b1.Id, rate1.BranchId);
        Assert.Equal(matGold.Id, rate1.MaterialId);
        Assert.Equal(pur22k.Id, rate1.PurityId);
        Assert.Equal("LKR", rate1.CurrencyCode);
        Assert.Equal(28500.5555m, rate1.RatePerGram);
        Assert.Equal(MetalRateSources.Manual, rate1.Source);
        Assert.True(rate1.IsActive);
        Assert.False(rate1.IsOverride);
        Assert.Null(rate1.EffectiveTo);

        // =========================================================================
        // 4. Single Active Rate Enforcement & Unbroken Timeline
        // =========================================================================
        _output.WriteLine("=== 4. Single Active Rate Enforcement ===");
        // Setting another manual rate for same tuple must supersede rate1
        var manualRateReq2 = new CreateManualRateRequest(
            BranchId: b1.Id,
            MaterialId: matGold.Id,
            PurityId: pur22k.Id,
            RatePerGram: 28700.1234m,
            CurrencyCode: "LKR",
            Notes: "Midday rate adjustment"
        );
        var createManualRes2 = await ownerClient.PostAsJsonAsync("/api/metal-rates/manual", manualRateReq2);
        Assert.Equal(HttpStatusCode.Created, createManualRes2.StatusCode);
        var rate2 = (await createManualRes2.Content.ReadFromJsonAsync<MetalRateDto>())!;

        Assert.Equal(28700.1234m, rate2.RatePerGram);
        Assert.True(rate2.IsActive);

        // Query active rates for branch: must contain exactly 1 rate for 22K gold
        var activeRatesRes = await ownerClient.GetFromJsonAsync<List<MetalRateDto>>($"/api/metal-rates?branchId={b1.Id}");
        Assert.NotNull(activeRatesRes);
        var active22k = activeRatesRes.Single(r => r.MaterialId == matGold.Id && r.PurityId == pur22k.Id);
        Assert.Equal(rate2.Id, active22k.Id);

        // Verify history shows rate1 was deactivated with unbroken EffectiveTo
        var historyRes = await ownerClient.GetFromJsonAsync<PagedResult<MetalRateDto>>(
            $"/api/metal-rates/history?branchId={b1.Id}&materialId={matGold.Id}&purityId={pur22k.Id}");
        Assert.NotNull(historyRes);
        Assert.True(historyRes.TotalCount >= 2);
        var oldVersion = historyRes.Items.First(r => r.Id == rate1.Id);
        Assert.False(oldVersion.IsActive);
        Assert.NotNull(oldVersion.EffectiveTo);

        // =========================================================================
        // 5. Branch Rate Isolation
        // =========================================================================
        _output.WriteLine("=== 5. Branch Rate Isolation ===");
        // Create CAD rate for Toronto Branch (T9B2)
        var b2RateRes = await ownerClient.PostAsJsonAsync("/api/metal-rates/manual", new CreateManualRateRequest(
            BranchId: b2.Id,
            MaterialId: matGold.Id,
            PurityId: pur22k.Id,
            RatePerGram: 125.7500m,
            CurrencyCode: "CAD",
            Notes: "Toronto daily CAD board"
        ));
        Assert.Equal(HttpStatusCode.Created, b2RateRes.StatusCode);
        var b2Rate = (await b2RateRes.Content.ReadFromJsonAsync<MetalRateDto>())!;

        // Verify B1 active rates only contain LKR and B2 contains CAD
        var b1Active = await ownerClient.GetFromJsonAsync<List<MetalRateDto>>($"/api/metal-rates?branchId={b1.Id}");
        var b2Active = await ownerClient.GetFromJsonAsync<List<MetalRateDto>>($"/api/metal-rates?branchId={b2.Id}");
        Assert.All(b1Active!, r => Assert.Equal("LKR", r.CurrencyCode));
        Assert.All(b2Active!, r => Assert.Equal("CAD", r.CurrencyCode));
        Assert.Equal(125.7500m, b2Active!.Single(r => r.PurityId == pur22k.Id).RatePerGram);

        // =========================================================================
        // 6. Admin Override with Mandatory Reason
        // =========================================================================
        _output.WriteLine("=== 6. Admin Override Validation & Preservation ===");
        // Attempt override without reason -> Must fail 400 Bad Request
        var badOverrideRes = await ownerClient.PostAsJsonAsync("/api/metal-rates/override", new AdminOverrideRateRequest(
            BranchId: b1.Id,
            MaterialId: matGold.Id,
            PurityId: pur22k.Id,
            OverrideRatePerGram: 28650.0000m,
            OverrideReason: "   " // whitespace only
        ));
        Assert.Equal(HttpStatusCode.BadRequest, badOverrideRes.StatusCode);

        // Successful Admin Override
        var validOverrideRes = await ownerClient.PostAsJsonAsync("/api/metal-rates/override", new AdminOverrideRateRequest(
            BranchId: b1.Id,
            MaterialId: matGold.Id,
            PurityId: pur22k.Id,
            OverrideRatePerGram: 28650.0000m,
            OverrideReason: "Management executive discretion for special festival week"
        ));
        Assert.Equal(HttpStatusCode.OK, validOverrideRes.StatusCode);
        var overrideRate = (await validOverrideRes.Content.ReadFromJsonAsync<MetalRateDto>())!;

        Assert.True(overrideRate.IsActive);
        Assert.True(overrideRate.IsOverride);
        Assert.Equal(MetalRateSources.AdminOverride, overrideRate.Source);
        Assert.Equal(28650.0000m, overrideRate.RatePerGram);
        Assert.Equal("Management executive discretion for special festival week", overrideRate.OverrideReason);
        // Original reference rate preserved
        Assert.Equal(rate2.RatePerGram, overrideRate.ExternalReferenceRate);

        // =========================================================================
        // 7. External Rate Provider Refresh & Subordinate Karat Derivation
        // =========================================================================
        _output.WriteLine("=== 7. External Provider Refresh & Karat Derivation ===");
        FakeProvider.ShouldFail = false;
        FakeProvider.Rate24K = 30000.0000m;
        // 22K is explicitly supplied: 27500.0000m
        // 18K is explicitly supplied: 22500.0000m

        var refreshRes = await ownerClient.PostAsJsonAsync("/api/metal-rates/refresh-external", new RefreshExternalRatesRequest(
            BranchId: b1.Id,
            CurrencyCode: "LKR"
        ));
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);
        var refreshedRates = (await refreshRes.Content.ReadFromJsonAsync<List<MetalRateDto>>())!;

        var active24k = refreshedRates.Single(r => r.MaterialId == matGold.Id && r.PurityId == pur24k.Id);
        var active22kAfterRefresh = refreshedRates.Single(r => r.MaterialId == matGold.Id && r.PurityId == pur22k.Id);
        var active18k = refreshedRates.Single(r => r.MaterialId == matGold.Id && r.PurityId == pur18k.Id);

        Assert.Equal(30000.0000m, active24k.RatePerGram);
        Assert.Equal(27500.0000m, active22kAfterRefresh.RatePerGram);
        Assert.Equal(22500.0000m, active18k.RatePerGram);
        Assert.Equal(MetalRateSources.ExternalApi, active24k.Source);
        Assert.Equal(30000.0000m, active22kAfterRefresh.ExternalReferenceRate);

        // Check provider telemetry
        var statusRes = await ownerClient.GetFromJsonAsync<ExternalRateProviderStatusDto>("/api/metal-rates/provider-status");
        Assert.NotNull(statusRes);
        Assert.True(statusRes.IsAvailable);
        Assert.NotNull(statusRes.LastSuccessfulRefreshAt);

        // =========================================================================
        // 8. Provider Outage Handling & Safe Rate Retention
        // =========================================================================
        _output.WriteLine("=== 8. Provider Outage Handling & Rate Retention ===");
        FakeProvider.ShouldFail = true;

        var failedRefreshRes = await ownerClient.PostAsJsonAsync("/api/metal-rates/refresh-external", new RefreshExternalRatesRequest(
            BranchId: b1.Id,
            CurrencyCode: "LKR"
        ));
        Assert.Equal(HttpStatusCode.OK, failedRefreshRes.StatusCode);
        var retainedRates = (await failedRefreshRes.Content.ReadFromJsonAsync<List<MetalRateDto>>())!;

        // Crucial Invariant: Outage must retain last valid rates untouched (NEVER zero out or deactivate)
        var retained24k = retainedRates.Single(r => r.MaterialId == matGold.Id && r.PurityId == pur24k.Id);
        var retained22k = retainedRates.Single(r => r.MaterialId == matGold.Id && r.PurityId == pur22k.Id);
        Assert.Equal(30000.0000m, retained24k.RatePerGram);
        Assert.Equal(27500.0000m, retained22k.RatePerGram);

        var statusAfterFailure = await ownerClient.GetFromJsonAsync<ExternalRateProviderStatusDto>("/api/metal-rates/provider-status");
        Assert.NotNull(statusAfterFailure);
        Assert.False(statusAfterFailure.IsAvailable);
        Assert.Contains("Simulated upstream provider outage", statusAfterFailure.LastErrorMessage);

        // =========================================================================
        // 9. Reference Pricing Foundation: Material + Making Charge (PerGram / PerItem)
        // =========================================================================
        _output.WriteLine("=== 9. Reference Pricing Foundation Calculations ===");
        // Case A: Making Charge PER_GRAM (Method = 1)
        // NetWeight = 12.5000g, Rate = 27500.0000, Making = 1500.00/g
        // Material Value = 12.5000 * 27500 = 343,750.00
        // Making Charge = 12.5000 * 1500 = 18,750.00
        // Subtotal = 362,500.00
        var calcReqA = new CalculateReferencePriceRequest(
            BranchId: b1.Id,
            MaterialId: matGold.Id,
            PurityId: pur22k.Id,
            NetWeight: 12.5000m,
            PieceCount: 1,
            MakingChargeMethod: 1, // PER_GRAM
            MakingChargeValue: 1500.00m,
            CurrencyCode: "LKR"
        );
        var calcResA = await ownerClient.PostAsJsonAsync("/api/pricing/calculate-reference", calcReqA);
        Assert.Equal(HttpStatusCode.OK, calcResA.StatusCode);
        var breakdownA = (await calcResA.Content.ReadFromJsonAsync<ReferencePriceBreakdownDto>())!;

        Assert.Equal(27500.0000m, breakdownA.RatePerGramUsed);
        Assert.Equal(343750.00m, breakdownA.ReferenceMaterialValue);
        Assert.Equal("PER_GRAM", breakdownA.MakingChargeMethodName);
        Assert.Equal(18750.00m, breakdownA.MakingChargeAmount);
        Assert.Equal(362500.00m, breakdownA.SubtotalBeforeTax);
        // Initially VAT is unconfigured/disabled -> VAT = 0.00
        Assert.False(breakdownA.VatEnabled);
        Assert.Equal(0.00m, breakdownA.VatAmount);
        Assert.Equal(362500.00m, breakdownA.ReferenceTotal);

        // Case B: Making Charge PER_ITEM (Method = 2)
        // NetWeight = 5.0000g, Rate = 27500.0000, PieceCount = 2, Making = 4000.00/item
        // Material Value = 5.0000 * 27500 = 137,500.00
        // Making Charge = 2 * 4000 = 8,000.00
        // Subtotal = 145,500.00
        var calcReqB = new CalculateReferencePriceRequest(
            BranchId: b1.Id,
            MaterialId: matGold.Id,
            PurityId: pur22k.Id,
            NetWeight: 5.0000m,
            PieceCount: 2,
            MakingChargeMethod: 2, // PER_ITEM
            MakingChargeValue: 4000.00m,
            CurrencyCode: "LKR"
        );
        var calcResB = await ownerClient.PostAsJsonAsync("/api/pricing/calculate-reference", calcReqB);
        Assert.Equal(HttpStatusCode.OK, calcResB.StatusCode);
        var breakdownB = (await calcResB.Content.ReadFromJsonAsync<ReferencePriceBreakdownDto>())!;

        Assert.Equal(137500.00m, breakdownB.ReferenceMaterialValue);
        Assert.Equal("PER_ITEM", breakdownB.MakingChargeMethodName);
        Assert.Equal(8000.00m, breakdownB.MakingChargeAmount);
        Assert.Equal(145500.00m, breakdownB.SubtotalBeforeTax);

        // =========================================================================
        // 10. VAT Configuration & Validation
        // =========================================================================
        _output.WriteLine("=== 10. VAT Configuration & Validation ===");
        // Invalid VAT percentage > 100% -> Rejected
        var badVatRes = await ownerClient.PostAsJsonAsync("/api/vat-configurations", new CreateVatConfigurationRequest(
            BranchId: b1.Id,
            Name: "Excessive VAT",
            IsEnabled: true,
            RatePercentage: 110.00m
        ));
        Assert.Equal(HttpStatusCode.BadRequest, badVatRes.StatusCode);

        // Valid VAT Configuration (18.00% standard rate, activated)
        var vatRes = await ownerClient.PostAsJsonAsync("/api/vat-configurations", new CreateVatConfigurationRequest(
            BranchId: b1.Id,
            Name: "Sri Lanka Standard VAT 2026",
            IsEnabled: true,
            RatePercentage: 18.00m,
            CalculationMode: VatCalculationModes.FlatPercentage
        ));
        Assert.Equal(HttpStatusCode.OK, vatRes.StatusCode);
        var vatConfig = (await vatRes.Content.ReadFromJsonAsync<VatConfigurationDto>())!;

        Assert.True(vatConfig.IsEnabled);
        Assert.Equal(18.00m, vatConfig.RatePercentage);
        Assert.Equal(VatCalculationModes.FlatPercentage, vatConfig.CalculationMode);
        Assert.True(vatConfig.IsActive);

        // =========================================================================
        // 11. Reference Pricing with Active VAT
        // =========================================================================
        _output.WriteLine("=== 11. Reference Pricing with Active VAT ===");
        // Subtotal = 362,500.00
        // VAT (18%) = 362,500.00 * 0.18 = 65,250.00
        // Reference Total = 427,750.00
        var calcResWithVat = await ownerClient.PostAsJsonAsync("/api/pricing/calculate-reference", calcReqA);
        Assert.Equal(HttpStatusCode.OK, calcResWithVat.StatusCode);
        var breakdownWithVat = (await calcResWithVat.Content.ReadFromJsonAsync<ReferencePriceBreakdownDto>())!;

        Assert.True(breakdownWithVat.VatEnabled);
        Assert.Equal(18.00m, breakdownWithVat.VatRatePercentage);
        Assert.Equal(65250.00m, breakdownWithVat.VatAmount);
        Assert.Equal(427750.00m, breakdownWithVat.ReferenceTotal);

        // =========================================================================
        // 12. VAT Deactivation & Re-check Rs. 0.00 Tax
        // =========================================================================
        _output.WriteLine("=== 12. VAT Deactivation & Zero Tax Verification ===");
        var toggleRes = await ownerClient.PostAsJsonAsync("/api/vat-configurations/toggle", new ToggleVatStatusRequest(
            BranchId: b1.Id,
            IsEnabled: false,
            Reason: "Tax holiday suspension test"
        ));
        Assert.Equal(HttpStatusCode.OK, toggleRes.StatusCode);
        var deactivatedVat = (await toggleRes.Content.ReadFromJsonAsync<VatConfigurationDto>())!;
        Assert.False(deactivatedVat.IsEnabled);

        // Re-calculate -> VAT must be 0.00
        var calcResNoVat = await ownerClient.PostAsJsonAsync("/api/pricing/calculate-reference", calcReqA);
        var breakdownNoVat = (await calcResNoVat.Content.ReadFromJsonAsync<ReferencePriceBreakdownDto>())!;
        Assert.False(breakdownNoVat.VatEnabled);
        Assert.Equal(0.00m, breakdownNoVat.VatAmount);
        Assert.Equal(362500.00m, breakdownNoVat.ReferenceTotal);

        // =========================================================================
        // 13. VAT History & Branch Isolation
        // =========================================================================
        _output.WriteLine("=== 13. VAT History & Branch Isolation ===");
        var vatHistoryRes = await ownerClient.GetFromJsonAsync<PagedResult<VatConfigurationDto>>(
            $"/api/vat-configurations/history?branchId={b1.Id}");
        Assert.NotNull(vatHistoryRes);
        Assert.True(vatHistoryRes.TotalCount >= 2);

        // Branch 2 Toronto has no VAT configured yet -> default disabled
        var b2VatRes = await ownerClient.GetFromJsonAsync<VatConfigurationDto>(
            $"/api/vat-configurations?branchId={b2.Id}");
        Assert.NotNull(b2VatRes);
        Assert.False(b2VatRes.IsEnabled);
        Assert.Equal(0.00m, b2VatRes.RatePercentage);

        // =========================================================================
        // 14. RBAC Permission Enforcement
        // =========================================================================
        _output.WriteLine("=== 14. RBAC Enforcement ===");
        var cashierUsername = $"test_m9_cashier_{Guid.NewGuid():N}"[..20];
        var cashierEmail = $"{cashierUsername}@saranga.test";
        var cashierUserRes = await ownerClient.PostAsJsonAsync("/api/users", new CreateUserRequest(
            Username: cashierUsername,
            Email: cashierEmail,
            Password: "CashierPassword@2026#",
            FullName: "Test M9 Cashier",
            DefaultBranchId: b1.Id,
            RoleIds: new List<Guid> { (await ownerClient.GetFromJsonAsync<List<RoleDto>>("/api/roles"))!.First(r => r.Name == "Cashier").Id },
            BranchIds: new List<Guid> { b1.Id }
        ));
        Assert.Equal(HttpStatusCode.Created, cashierUserRes.StatusCode);

        var cashierLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(cashierUsername, "CashierPassword@2026#"));
        var cashierToken = (await cashierLoginRes.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var cashierClient = _factory.CreateClient();
        cashierClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierToken);

        // Cashier attempts manual rate creation -> Must be 403 Forbidden
        var unauthManualRes = await cashierClient.PostAsJsonAsync("/api/metal-rates/manual", manualRateReq);
        Assert.Equal(HttpStatusCode.Forbidden, unauthManualRes.StatusCode);

        // Cashier attempts admin override -> Must be 403 Forbidden
        var unauthOverrideRes = await cashierClient.PostAsJsonAsync("/api/metal-rates/override", new AdminOverrideRateRequest(
            BranchId: b1.Id,
            MaterialId: matGold.Id,
            PurityId: pur22k.Id,
            OverrideRatePerGram: 29000.0000m,
            OverrideReason: "Unauthorized attempt"
        ));
        Assert.Equal(HttpStatusCode.Forbidden, unauthOverrideRes.StatusCode);

        // Cashier attempts VAT toggle -> Must be 403 Forbidden
        var unauthVatRes = await cashierClient.PostAsJsonAsync("/api/vat-configurations/toggle", new ToggleVatStatusRequest(
            BranchId: b1.Id,
            IsEnabled: true
        ));
        Assert.Equal(HttpStatusCode.Forbidden, unauthVatRes.StatusCode);

        // Cashier can read active rates -> 200 OK
        var cashierRatesRes = await cashierClient.GetAsync($"/api/metal-rates?branchId={b1.Id}");
        Assert.Equal(HttpStatusCode.OK, cashierRatesRes.StatusCode);

        _output.WriteLine("=== All Module 9 Verification Phases Completed Successfully ===");
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Api;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.DTOs.Pos;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class Module13IntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Module13IntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
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
            SET LOCAL saranga.maintenance_mode = 'true';
            DELETE FROM ""InvoicePayments"" WHERE ""InvoiceId"" IN (SELECT ""Id"" FROM ""Invoices"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%'));
            DELETE FROM ""InvoiceLines"" WHERE ""InvoiceId"" IN (SELECT ""Id"" FROM ""Invoices"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%'));
            DELETE FROM ""Invoices"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""InventoryCompositionMembers"" WHERE ""CompositionId"" IN (SELECT ""Id"" FROM ""InventoryCompositions"" WHERE ""TagNumber"" LIKE '%T13%');
            DELETE FROM ""InventoryCompositions"" WHERE ""TagNumber"" LIKE '%T13%';
            DELETE FROM ""CashLedgerEntries"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""CashLedgerTransactions"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""Shifts"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""CashDrawers"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""InventoryItems"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""Customers"" WHERE ""CustomerCode"" LIKE 'CUST-T13%';
            DELETE FROM ""MetalRates"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""StockLocations"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""UserBranches"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""VatConfigurations"" WHERE ""BranchId"" IN (SELECT ""Id"" FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%');
            DELETE FROM ""Branches"" WHERE ""Code"" LIKE 'T13_%';
            DELETE FROM ""IdempotencyRecords"" WHERE ""Key"" LIKE 'POS-%';
        ");
    }

    private async Task<string> LoginAsOwnerAsync(HttpClient client)
    {
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            Username: "saranga_owner",
            Password: "OwnerSuperAdmin@2026#Secure"
        ));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loginData = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginData);
        return loginData.AccessToken;
    }

    private async Task<(Branch Branch, CashDrawer Drawer, Shift Shift, Customer Customer, Material Material, Purity Purity, Category Cat, InventoryItem InvItem1, InventoryItem InvItem2, StockLocation Loc, User OwnerUser)> SetupSeedDataAsync(string testKey, bool enableBranchVat = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = await db.Users.FirstAsync(u => u.Username == "saranga_owner");

        var branch = new Branch
        {
            Name = $"Module 13 POS {testKey}",
            Code = $"T13_{testKey}",
            Country = "Sri Lanka",
            CurrencyCode = "LKR",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        db.UserBranches.Add(new UserBranch { UserId = owner.Id, BranchId = branch.Id, IsDefault = false });

        var stockLoc = new StockLocation
        {
            BranchId = branch.Id,
            Name = $"Main Display {testKey}",
            Code = $"T13_LOC_{testKey}",
            Description = "Main Display Showcase",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.StockLocations.Add(stockLoc);

        var drawer = new CashDrawer
        {
            BranchId = branch.Id,
            DrawerCode = $"DRW_{testKey}",
            Name = $"POS Drawer {testKey}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.CashDrawers.Add(drawer);
        await db.SaveChangesAsync();

        var shift = new Shift
        {
            BranchId = branch.Id,
            CashDrawerId = drawer.Id,
            CashierId = owner.Id,
            ShiftNumber = $"SHF-T13-{testKey}-01",
            OpenedAt = DateTimeOffset.UtcNow,
            OpenedBy = owner.Id,
            OpeningBalance = 50000.00m,
            SystemExpectedOpeningBalance = 50000.00m,
            ExpectedClosingBalance = 50000.00m,
            Status = ShiftStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        db.Shifts.Add(shift);

        var phoneDigits = (Math.Abs(testKey.GetHashCode()) % 900000 + 100000).ToString();
        var customer = new Customer
        {
            CustomerCode = $"CUST-T13{testKey}",
            FullName = $"Amara {testKey}",
            PrimaryPhone = $"077{phoneDigits}",
            NormalizedPrimaryPhone = $"+9477{phoneDigits}",
            Country = "Sri Lanka",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);

        var material = await db.Materials.FirstOrDefaultAsync(m => m.Code == "AU");
        if (material == null)
        {
            material = new Material { Name = "Gold", Code = "AU", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Materials.Add(material);
            await db.SaveChangesAsync();
        }

        var purity = await db.Purities.FirstOrDefaultAsync(p => p.Code == "22K");
        if (purity == null)
        {
            purity = new Purity { Name = "22 Karat", Code = "22K", KaratValue = 22m, PurityPercentage = 91.67m, IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Purities.Add(purity);
            await db.SaveChangesAsync();
        }

        var cat = await db.Categories.FirstOrDefaultAsync(c => c.Code == "RNG");
        if (cat == null)
        {
            cat = new Category { Name = "Rings", Code = "RNG", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Categories.Add(cat);
            await db.SaveChangesAsync();
        }

        // Add metal rate for branch & purity
        var metalRate = new MetalRate
        {
            BranchId = branch.Id,
            MaterialId = material.Id,
            PurityId = purity.Id,
            CurrencyCode = "LKR",
            RatePerGram = 25000.00m,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            IsActive = true,
            CreatedBy = owner.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MetalRates.Add(metalRate);

        // Add inventory items
        var item1 = new InventoryItem
        {
            BranchId = branch.Id,
            StockLocationId = stockLoc.Id,
            MaterialId = material.Id,
            PurityId = purity.Id,
            CategoryId = cat.Id,
            Name = $"22K Gold Ring 8g {testKey}",
            ItemCode = $"T13-SKU-01-{testKey}",
            TagNumber = $"T13-RNG-01-{testKey}",
            Barcode = $"T1301BAR{testKey}",
            GrossWeight = 8.000m,
            NetWeight = 8.000m,
            StoneWeight = 0m,
            PieceCount = 1,
            MakingChargeMethod = MakingChargeMethod.PerGram,
            MakingChargeValue = 1500.00m, // 8 * 1500 = 12,000
            Status = InventoryStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        var item2 = new InventoryItem
        {
            BranchId = branch.Id,
            StockLocationId = stockLoc.Id,
            MaterialId = material.Id,
            PurityId = purity.Id,
            CategoryId = cat.Id,
            Name = $"22K Gold Ring 5g {testKey}",
            ItemCode = $"T13-SKU-02-{testKey}",
            TagNumber = $"T13-RNG-02-{testKey}",
            Barcode = $"T1302BAR{testKey}",
            GrossWeight = 5.000m,
            NetWeight = 5.000m,
            StoneWeight = 0m,
            PieceCount = 1,
            MakingChargeMethod = MakingChargeMethod.PerItem,
            MakingChargeValue = 10000.00m, // Fixed 10,000
            Status = InventoryStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        db.InventoryItems.AddRange(item1, item2);
        await db.SaveChangesAsync();

        return (branch, drawer, shift, customer, material, purity, cat, item1, item2, stockLoc, owner);
    }

    [Fact]
    public async Task Pos_01_LookupItem_CalculatesAccurateLiveReferencePricing()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("01");

        // Material value = 8g * 25,000 = 200,000
        // Making charge = 8g * 1,500 = 12,000
        // Reference total = 212,000
        var lookupRes = await client.GetAsync($"/api/pos/lookup?shiftId={data.Shift.Id}&query={data.InvItem1.TagNumber}");
        Assert.Equal(HttpStatusCode.OK, lookupRes.StatusCode);

        var item = await lookupRes.Content.ReadFromJsonAsync<PosItemLookupResultDto>(JsonOptions);
        Assert.NotNull(item);
        Assert.Equal(data.InvItem1.Id, item.InventoryItemId);
        Assert.Equal(data.InvItem1.TagNumber, item.TagNumber);
        Assert.Equal(25000.00m, item.ActiveRatePerGram);
        Assert.Equal(200000.00m, item.ReferenceMaterialValue);
        Assert.Equal(12000.00m, item.ReferenceMakingCharge);
        Assert.Equal(212000.00m, item.ReferencePrice);
    }

    [Fact]
    public async Task Pos_02_DraftCreation_AndQuickCustomerWalkIn()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("02");

        // 1. Create draft with existing customer
        var createDraftRes = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: "Test Draft 1"
        ));
        Assert.Equal(HttpStatusCode.Created, createDraftRes.StatusCode);
        var draft1 = await createDraftRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft1);
        Assert.Equal(data.Customer.Id, draft1.CustomerId);
        Assert.Equal(0, draft1.Status); // Draft

        // 2. Quick create walk-in customer with draft
        var quickRes = await client.PostAsJsonAsync("/api/pos/drafts/quick-customer", new QuickCreateCustomerAndDraftRequest(
            ShiftId: data.Shift.Id,
            FullName: "Kamal Perera",
            PrimaryPhone: "077 999 1333", // local formatting
            Email: null,
            AddressLine1: null,
            City: "Colombo",
            Notes: null
        ));
        Assert.Equal(HttpStatusCode.Created, quickRes.StatusCode);
        var quickDraft = await quickRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(quickDraft);
        Assert.Equal("Kamal Perera", quickDraft.CustomerName);
        Assert.Equal("077 999 1333", quickDraft.CustomerPhone); // User-entered display phone preserved

        // Verify normalized primary phone in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cust = await db.Customers.FindAsync(quickDraft.CustomerId);
            Assert.NotNull(cust);
            Assert.Equal("+94779991333", cust.NormalizedPrimaryPhone);
        }
    }

    [Fact]
    public async Task Pos_03_AddUpdateRemoveCartLines()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("03");

        var draftRes = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: null
        ));
        var draft = await draftRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft);

        // Add line
        var addRes = await client.PostAsJsonAsync($"/api/pos/invoices/{draft.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem1.TagNumber,
            NegotiatedUnitPrice: 210000.00m, // Negotiated discount from 212,000
            Notes: null
        ));
        Assert.Equal(HttpStatusCode.OK, addRes.StatusCode);
        var invoiceWithLine = await addRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(invoiceWithLine);
        Assert.Single(invoiceWithLine.Lines);
        var line = invoiceWithLine.Lines[0];
        Assert.Equal(212000.00m, line.ReferencePrice);
        Assert.Equal(210000.00m, line.UnitPrice);

        // Update line price
        var updateRes = await client.PutAsJsonAsync($"/api/pos/invoices/{draft.Id}/lines/{line.Id}", new UpdatePosInvoiceLineRequest(
            NegotiatedUnitPrice: 208000.00m,
            Notes: null
        ));
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedInv = await updateRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(updatedInv);
        Assert.Equal(208000.00m, updatedInv.Lines[0].UnitPrice);

        // Remove line
        var removeRes = await client.DeleteAsync($"/api/pos/invoices/{draft.Id}/lines/{line.Id}");
        Assert.Equal(HttpStatusCode.OK, removeRes.StatusCode);
        var emptyInv = await removeRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(emptyInv);
        Assert.Empty(emptyInv.Lines);
    }

    [Fact]
    public async Task Pos_04_NegotiatedPrice_AboveAndBelowReference_IsPreserved()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("04");

        var draftRes = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: null
        ));
        var draft = await draftRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft);

        // Add item 1 with negotiated price LOWER than reference (205,000 vs 212,000)
        await client.PostAsJsonAsync($"/api/pos/invoices/{draft.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem1.TagNumber,
            NegotiatedUnitPrice: 205000.00m,
            Notes: null
        ));

        // Add item 2 with negotiated price HIGHER than reference (item2 ref: 5g * 25k = 125k + 10k fixed = 135k; negotiated: 140,000)
        var add2Res = await client.PostAsJsonAsync($"/api/pos/invoices/{draft.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem2.TagNumber,
            NegotiatedUnitPrice: 140000.00m,
            Notes: null
        ));
        var inv = await add2Res.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(inv);
        Assert.Equal(2, inv.Lines.Count);

        var line1 = inv.Lines.First(l => l.InventoryItemId == data.InvItem1.Id);
        var line2 = inv.Lines.First(l => l.InventoryItemId == data.InvItem2.Id);

        Assert.Equal(212000.00m, line1.ReferencePrice);
        Assert.Equal(205000.00m, line1.UnitPrice);

        Assert.Equal(135000.00m, line2.ReferencePrice);
        Assert.Equal(140000.00m, line2.UnitPrice);

        // Subtotal = 205,000 + 140,000 = 345,000
        Assert.Equal(345000.00m, inv.SubTotal);
        Assert.Equal(345000.00m, inv.TotalAmount);
    }

    [Fact]
    public async Task Pos_05_InventoryConcurrency_SoldItemCannotBeSoldTwice()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("05");

        // 1. Create and finalize invoice 1 with item 1
        var draft1Res = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: null
        ));
        var draft1 = await draft1Res.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft1);

        await client.PostAsJsonAsync($"/api/pos/invoices/{draft1.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem1.TagNumber,
            NegotiatedUnitPrice: 212000.00m,
            Notes: null
        ));

        var finalize1Req = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft1.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 212000.00m, CashTendered: 212000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        finalize1Req.Headers.Add("Idempotency-Key", $"POS-FIN-1-{Guid.NewGuid():N}");
        var finalize1Res = await client.SendAsync(finalize1Req);
        Assert.Equal(HttpStatusCode.OK, finalize1Res.StatusCode);

        // Verify item status transitioned to Sold
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item = await db.InventoryItems.FindAsync(data.InvItem1.Id);
            Assert.NotNull(item);
            Assert.Equal(InventoryStatus.Sold, item.Status);
        }

        // 2. Attempt to add sold item in a second invoice
        var draft2Res = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: null
        ));
        var draft2 = await draft2Res.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft2);

        var addSoldRes = await client.PostAsJsonAsync($"/api/pos/invoices/{draft2.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem1.TagNumber,
            NegotiatedUnitPrice: 212000.00m,
            Notes: null
        ));
        // Must be rejected with Conflict as item is already Sold
        Assert.Equal(HttpStatusCode.BadRequest, addSoldRes.StatusCode);
    }

    [Fact]
    public async Task Pos_06_CompositionAutoDetachment_OnSale()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("06");

        // Create a Group Composition and add InvItem1 as a Member
        Guid compId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var composition = new InventoryComposition
            {
                BranchId = data.Branch.Id,
                StockLocationId = data.Loc.Id,
                Name = "Bridal Set Group",
                Type = CompositionType.Group,
                TagNumber = $"GRP-T13-06",
                Barcode = $"GRPT13BAR06",
                Status = CompositionStatus.Active,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.InventoryCompositions.Add(composition);
            await db.SaveChangesAsync();
            compId = composition.Id;

            var member = new InventoryCompositionMember
            {
                CompositionId = compId,
                InventoryItemId = data.InvItem1.Id,
                SortOrder = 1,
                IsActive = true,
                AddedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            db.InventoryCompositionMembers.Add(member);
            await db.SaveChangesAsync();
        }

        // Create draft and finalize sale of Member Item 1
        var draftRes = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: null
        ));
        var draft = await draftRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft);

        await client.PostAsJsonAsync($"/api/pos/invoices/{draft.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem1.TagNumber,
            NegotiatedUnitPrice: 212000.00m,
            Notes: null
        ));

        var finReq = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 212000.00m, CashTendered: 212000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        finReq.Headers.Add("Idempotency-Key", $"POS-FIN-GRP-{Guid.NewGuid():N}");
        var finRes = await client.SendAsync(finReq);
        Assert.Equal(HttpStatusCode.OK, finRes.StatusCode);

        // Verify that Item 1 has been detached from Composition (membership IsActive is false, RemovedAt is set)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item1 = await db.InventoryItems.FindAsync(data.InvItem1.Id);
            Assert.NotNull(item1);
            Assert.Equal(InventoryStatus.Sold, item1.Status);

            var member = await db.InventoryCompositionMembers
                .FirstOrDefaultAsync(m => m.CompositionId == compId && m.InventoryItemId == data.InvItem1.Id);
            Assert.NotNull(member);
            Assert.False(member.IsActive);
            Assert.NotNull(member.RemovedAt);
        }
    }

    [Fact]
    public async Task Pos_07_SplitPayments_CashCardBankTransfer()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("07");

        var draftRes = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: null
        ));
        var draft = await draftRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft);

        // Add line: 212,000
        await client.PostAsJsonAsync($"/api/pos/invoices/{draft.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem1.TagNumber,
            NegotiatedUnitPrice: 212000.00m,
            Notes: null
        ));

        // Split payments:
        // Cash: 50,000 (Tendered 60,000 -> Change 10,000)
        // Card: 100,000 (Visa *4321)
        // Bank Transfer: 62,000 (Ref #TXN987654)
        // Total = 50k + 100k + 62k = 212,000
        var finReq = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 50000.00m, CashTendered: 60000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null),
                    new(Method: PaymentMethod.Card, Amount: 100000.00m, CashTendered: null, ReferenceNumber: "AUTH123456", CardType: "Visa", CardLast4: "4321", BankName: null, Notes: null),
                    new(Method: PaymentMethod.BankTransfer, Amount: 62000.00m, CashTendered: null, ReferenceNumber: "TXN987654", CardType: null, CardLast4: null, BankName: "Commercial Bank", Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        finReq.Headers.Add("Idempotency-Key", $"POS-FIN-SPLIT-{Guid.NewGuid():N}");
        var finRes = await client.SendAsync(finReq);
        Assert.Equal(HttpStatusCode.OK, finRes.StatusCode);

        var finalInv = await finRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(finalInv);
        Assert.Equal(1, finalInv.Status); // Finalized
        Assert.Equal(212000.00m, finalInv.TotalPaidAmount);
        Assert.Equal(10000.00m, finalInv.TotalCashChange);
        Assert.Equal(3, finalInv.Payments.Count);

        var cashPay = finalInv.Payments.First(p => p.PaymentMethod == 1);
        var cardPay = finalInv.Payments.First(p => p.PaymentMethod == 2);
        var bankPay = finalInv.Payments.First(p => p.PaymentMethod == 3);

        Assert.Equal(50000.00m, cashPay.Amount);
        Assert.Equal(60000.00m, cashPay.CashTendered);
        Assert.Equal(10000.00m, cashPay.ChangeGiven);

        Assert.Equal(100000.00m, cardPay.Amount);
        Assert.Equal("Visa", cardPay.CardType);
        Assert.Equal("4321", cardPay.CardLast4);

        Assert.Equal(62000.00m, bankPay.Amount);
        Assert.Equal("TXN987654", bankPay.ReferenceNumber);
    }

    [Fact]
    public async Task Pos_08_CashLedgerIntegration_NetCashPosted()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("08");

        var draftRes = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: null
        ));
        var draft = await draftRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft);

        await client.PostAsJsonAsync($"/api/pos/invoices/{draft.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem1.TagNumber,
            NegotiatedUnitPrice: 212000.00m,
            Notes: null
        ));

        // Pay 150,000 cash (Tendered 160,000 -> 10,000 change -> Net Cash 150,000)
        // Pay 62,000 card
        var finReq = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 150000.00m, CashTendered: 160000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null),
                    new(Method: PaymentMethod.Card, Amount: 62000.00m, CashTendered: null, ReferenceNumber: null, CardType: "MasterCard", CardLast4: "9999", BankName: null, Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        finReq.Headers.Add("Idempotency-Key", $"POS-FIN-LEDGER-{Guid.NewGuid():N}");
        var finRes = await client.SendAsync(finReq);
        Assert.Equal(HttpStatusCode.OK, finRes.StatusCode);

        // Verify physical Cash Ledger transaction and entries
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Find Cash Ledger Transaction linked to this invoice reference
            var ledgerTxn = await db.CashLedgerTransactions
                .Include(t => t.Entries)
                .FirstOrDefaultAsync(t => t.SourceReferenceId == draft.InvoiceNumber);

            Assert.NotNull(ledgerTxn);
            Assert.Equal(CashLedgerTransactionType.CashIn, ledgerTxn.TransactionType);
            Assert.Equal(150000.00m, ledgerTxn.Amount); // Net cash amount

            // Exactly 1 entry for single drawer cash in
            Assert.Single(ledgerTxn.Entries);
            var entry = ledgerTxn.Entries.First();
            Assert.Equal(CashLedgerEntryDirection.In, entry.Direction);
            Assert.Equal(150000.00m, entry.Amount);
            Assert.Equal(data.Drawer.Id, entry.CashDrawerId);

            // Verify shift live expected cash balance increased by 150,000 (from 50,000 to 200,000)
            var expectedBalanceService = scope.ServiceProvider.GetRequiredService<IShiftExpectedBalanceService>();
            var expectedBalance = await expectedBalanceService.CalculateExpectedClosingBalanceAsync(data.Shift.Id, data.Shift.OpeningBalance);
            Assert.Equal(200000.00m, expectedBalance);
        }
    }

    [Fact]
    public async Task Pos_09_MandatoryIdempotency_AndConcurrentFinalization()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var data = await SetupSeedDataAsync("09");

        var draftRes = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: data.Shift.Id,
            CustomerId: data.Customer.Id,
            Notes: null
        ));
        var draft = await draftRes.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft);

        await client.PostAsJsonAsync($"/api/pos/invoices/{draft.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: data.InvItem1.TagNumber,
            NegotiatedUnitPrice: 212000.00m,
            Notes: null
        ));

        // 1. Missing Idempotency-Key header must be rejected
        var noKeyReq = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 212000.00m, CashTendered: 212000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        var noKeyRes = await client.SendAsync(noKeyReq);
        Assert.Equal(HttpStatusCode.BadRequest, noKeyRes.StatusCode);

        // 2. Supply key and finalize
        var idemKey = $"POS-IDEM-{Guid.NewGuid():N}";
        var keyReq1 = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 212000.00m, CashTendered: 212000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        keyReq1.Headers.Add("Idempotency-Key", idemKey);
        var res1 = await client.SendAsync(keyReq1);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var inv1 = await res1.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(inv1);
        Assert.Equal(1, inv1.Status);

        // 3. Replay with identical key must return original successful response without double posting
        var keyReq2 = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 212000.00m, CashTendered: 212000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        keyReq2.Headers.Add("Idempotency-Key", idemKey);
        var res2 = await client.SendAsync(keyReq2);
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var inv2 = await res2.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(inv2);
        Assert.Equal(inv1.Id, inv2.Id);
        Assert.Equal(inv1.InvoiceNumber, inv2.InvoiceNumber);

        // Verify in database that only ONE cash ledger transaction exists for this invoice
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var txnCount = await db.CashLedgerTransactions.CountAsync(t => t.SourceReferenceId == inv1.InvoiceNumber);
            Assert.Equal(1, txnCount);
        }
    }

    [Fact]
    public async Task Pos_10_VatDisabled_AllowsSale_VatEnabled_SafelyRejectsWithPendingMessage()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsOwnerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. VAT Disabled Branch: Sale succeeds
        var dataNoVat = await SetupSeedDataAsync("10A", enableBranchVat: false);

        var draft1Res = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: dataNoVat.Shift.Id,
            CustomerId: dataNoVat.Customer.Id,
            Notes: null
        ));
        var draft1 = await draft1Res.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft1);

        await client.PostAsJsonAsync($"/api/pos/invoices/{draft1.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: dataNoVat.InvItem1.TagNumber,
            NegotiatedUnitPrice: 212000.00m,
            Notes: null
        ));

        var fin1Req = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft1.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 212000.00m, CashTendered: 212000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        fin1Req.Headers.Add("Idempotency-Key", $"POS-VAT-OFF-{Guid.NewGuid():N}");
        var fin1Res = await client.SendAsync(fin1Req);
        Assert.Equal(HttpStatusCode.OK, fin1Res.StatusCode);

        // Close Shift 1 before opening Shift 2 for the same cashier (enforces 1 active shift per cashier rule)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var s1 = await db.Shifts.FindAsync(dataNoVat.Shift.Id);
            s1!.Status = ShiftStatus.Closed;
            s1.ClosedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        // 2. VAT Enabled Branch: Finalization is safely blocked with a clear message
        var dataWithVat = await SetupSeedDataAsync("10B", enableBranchVat: true);

        // Set up VAT configuration for this branch
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vatConfig = new VatConfiguration
            {
                BranchId = dataWithVat.Branch.Id,
                IsEnabled = true,
                RatePercentage = 18.00m,
                CalculationMode = VatCalculationModes.FlatPercentage,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.VatConfigurations.Add(vatConfig);
            await db.SaveChangesAsync();
        }

        var draft2Res = await client.PostAsJsonAsync("/api/pos/drafts", new CreatePosDraftInvoiceRequest(
            ShiftId: dataWithVat.Shift.Id,
            CustomerId: dataWithVat.Customer.Id,
            Notes: null
        ));
        var draft2 = await draft2Res.Content.ReadFromJsonAsync<PosInvoiceDto>(JsonOptions);
        Assert.NotNull(draft2);

        await client.PostAsJsonAsync($"/api/pos/invoices/{draft2.Id}/lines", new AddPosInvoiceLineRequest(
            ItemBarcodeOrTag: dataWithVat.InvItem1.TagNumber,
            NegotiatedUnitPrice: 212000.00m,
            Notes: null
        ));

        var fin2Req = new HttpRequestMessage(HttpMethod.Post, $"/api/pos/invoices/{draft2.Id}/finalize")
        {
            Content = JsonContent.Create(new FinalizePosInvoiceRequest(
                Payments: new List<PosPaymentRequest>
                {
                    new(Method: PaymentMethod.Cash, Amount: 212000.00m, CashTendered: 212000.00m, ReferenceNumber: null, CardType: null, CardLast4: null, BankName: null, Notes: null)
                },
                DiscountAmount: null,
                Notes: null
            ))
        };
        fin2Req.Headers.Add("Idempotency-Key", $"POS-VAT-ON-{Guid.NewGuid():N}");
        var fin2Res = await client.SendAsync(fin2Req);
        Assert.Equal(HttpStatusCode.BadRequest, fin2Res.StatusCode);

        var errorBody = await fin2Res.Content.ReadAsStringAsync();
        Assert.Contains("VAT calculation treatment", errorBody);
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Pos;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public class PosService : IPosService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchAuthorizationService _branchAuthService;
    private readonly ITagGeneratorService _tagGenerator;
    private readonly IReferencePricingService _pricingService;
    private readonly IMetalRateService _metalRateService;
    private readonly IVatConfigurationService _vatService;
    private readonly ICashLedgerService _cashLedgerService;
    private readonly ICompositionService _compositionService;
    private readonly IPhoneNormalizationService _phoneService;
    private readonly ILogger<PosService> _logger;

    public PosService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IBranchAuthorizationService branchAuthService,
        ITagGeneratorService tagGenerator,
        IReferencePricingService pricingService,
        IMetalRateService metalRateService,
        IVatConfigurationService vatService,
        ICashLedgerService cashLedgerService,
        ICompositionService compositionService,
        IPhoneNormalizationService phoneService,
        ILogger<PosService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _branchAuthService = branchAuthService;
        _tagGenerator = tagGenerator;
        _pricingService = pricingService;
        _metalRateService = metalRateService;
        _vatService = vatService;
        _cashLedgerService = cashLedgerService;
        _compositionService = compositionService;
        _phoneService = phoneService;
        _logger = logger;
    }

    public async Task<PosItemLookupResultDto> LookupItemForSaleAsync(Guid shiftId, string barcodeOrTag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcodeOrTag))
        {
            throw new BusinessRuleException("Barcode or tag number is required.");
        }

        var shift = await _context.Shifts
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == shiftId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Shift", shiftId);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException($"Shift '{shift.ShiftNumber}' is closed.");
        }

        var cleanQuery = barcodeOrTag.Trim();

        // 1. Locate physical inventory item
        var item = await _context.InventoryItems
            .Include(i => i.Category)
            .Include(i => i.Material)
            .Include(i => i.Purity)
            .FirstOrDefaultAsync(i =>
                (i.Barcode == cleanQuery || i.TagNumber.ToLower() == cleanQuery.ToLower() || i.ItemCode.ToLower() == cleanQuery.ToLower()) &&
                !i.IsDeleted,
                cancellationToken);

        if (item == null)
        {
            // Check if it's a composition barcode/tag
            var comp = await _context.InventoryCompositions
                .Include(c => c.Members.Where(m => m.IsActive))
                    .ThenInclude(m => m.InventoryItem)
                .FirstOrDefaultAsync(c =>
                    (c.Barcode == cleanQuery || c.TagNumber.ToLower() == cleanQuery.ToLower()) &&
                    !c.IsDeleted,
                    cancellationToken);

            if (comp != null)
            {
                throw new BusinessRuleException($"'{cleanQuery}' is a composition parent ({comp.Type}). In accordance with physical stock integrity rules, composition parent records cannot be sold directly. Please scan or add its individual active child items.");
            }

            throw new NotFoundException($"No inventory item found matching '{cleanQuery}'.");
        }

        if (item.Status != InventoryStatus.Available)
        {
            throw new BusinessRuleException($"Item '{item.TagNumber}' cannot be sold because its current status is {item.Status}. Only Available items can be added to a sale.");
        }

        if (item.BranchId != shift.BranchId)
        {
            throw new BusinessRuleException($"Item '{item.TagNumber}' belongs to a different branch and cannot be sold in this shift.");
        }

        // 2. Check composition membership
        var membership = await _context.InventoryCompositionMembers
            .Include(m => m.Composition)
            .FirstOrDefaultAsync(m => m.InventoryItemId == item.Id && m.IsActive, cancellationToken);

        // 3. Calculate reference price
        decimal ratePerGram = 0m;
        decimal refMaterialVal = 0m;
        decimal refMakingCharge = 0m;
        decimal refPrice = 0m;

        try
        {
            var pricing = await _pricingService.CalculateReferencePriceAsync(new Application.DTOs.Rates.CalculateReferencePriceRequest(
                shift.BranchId,
                item.MaterialId,
                item.PurityId,
                item.NetWeight,
                item.PieceCount,
                (int)item.MakingChargeMethod,
                item.MakingChargeValue,
                shift.CurrencyCode
            ), cancellationToken);

            ratePerGram = pricing.RatePerGramUsed;
            refMaterialVal = pricing.ReferenceMaterialValue;
            refMakingCharge = pricing.MakingChargeAmount;
            refPrice = pricing.ReferenceTotal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reference price could not be computed for item {TagNumber}. Using 0 as reference price.", item.TagNumber);
        }

        return new PosItemLookupResultDto(
            item.Id,
            item.ItemCode,
            item.TagNumber,
            item.Barcode,
            item.Name,
            item.CategoryId,
            item.Category?.Name ?? string.Empty,
            item.MaterialId,
            item.Material?.Name ?? string.Empty,
            item.PurityId,
            item.Purity?.Name ?? string.Empty,
            item.Purity?.KaratValue ?? 0m,
            item.GrossWeight,
            item.StoneWeight,
            item.NetWeight,
            item.PieceCount,
            ratePerGram,
            refMaterialVal,
            refMakingCharge,
            refPrice,
            shift.CurrencyCode,
            membership != null,
            membership?.CompositionId,
            membership?.Composition?.Name,
            membership?.Composition?.TagNumber,
            membership?.Composition?.Type.ToString()
        );
    }

    public async Task<PosInvoiceDto> CreateDraftInvoiceAsync(CreatePosDraftInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var shift = await _context.Shifts
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Shift", request.ShiftId);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException($"Shift '{shift.ShiftNumber}' is closed. Sales can only be initiated on open shifts.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId && !c.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        if (!customer.IsActive)
        {
            throw new BusinessRuleException($"Customer '{customer.FullName}' is inactive.");
        }

        var invNumber = await _tagGenerator.GenerateInvoiceNumberAsync(shift.Branch?.Code ?? "BR", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var cashierId = _currentUserService.UserId ?? shift.CashierId;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invNumber,
            BranchId = shift.BranchId,
            ShiftId = shift.Id,
            CashierId = cashierId,
            CustomerId = customer.Id,
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PrimaryPhone,
            Status = InvoiceStatus.Draft,
            SubTotal = 0m,
            DiscountAmount = 0m,
            VatRate = 0m,
            VatAmount = 0m,
            TotalAmount = 0m,
            CurrencyCode = shift.CurrencyCode,
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            CreatedBy = cashierId
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetInvoiceByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<PosInvoiceDto> QuickCreateCustomerAndDraftAsync(QuickCreateCustomerAndDraftRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new BusinessRuleException("Customer full name is mandatory.");
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryPhone))
        {
            throw new BusinessRuleException("Customer primary phone number is mandatory.");
        }

        var shift = await _context.Shifts
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId && !s.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Shift", request.ShiftId);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException($"Shift '{shift.ShiftNumber}' is closed.");
        }

        var region = "LK";
        if (!string.IsNullOrWhiteSpace(shift.Branch?.Country))
        {
            var country = shift.Branch.Country.Trim();
            if (country.Length == 2)
            {
                region = country.ToUpperInvariant();
            }
            else if (country.Equals("Sri Lanka", StringComparison.OrdinalIgnoreCase))
            {
                region = "LK";
            }
        }
        var normalizedPhone = _phoneService.Normalize(request.PrimaryPhone, region);

        // Check if customer already exists by normalized phone
        var existingCustomer = await _context.Customers
            .FirstOrDefaultAsync(c => c.NormalizedPrimaryPhone == normalizedPhone && !c.IsDeleted, cancellationToken);

        Customer customer;
        if (existingCustomer != null)
        {
            customer = existingCustomer;
            if (!customer.IsActive)
            {
                customer.IsActive = true;
                customer.UpdatedAt = DateTimeOffset.UtcNow;
                customer.UpdatedBy = _currentUserService.UserId;
            }
        }
        else
        {
            var customerCode = await _tagGenerator.GenerateCustomerCodeAsync(cancellationToken);
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                CustomerCode = customerCode,
                FullName = request.FullName.Trim(),
                PrimaryPhone = request.PrimaryPhone.Trim(),
                NormalizedPrimaryPhone = normalizedPhone,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
                AddressLine1 = request.AddressLine1?.Trim(),
                City = request.City?.Trim(),
                Country = shift.Branch?.Country ?? "Sri Lanka",
                Notes = request.Notes?.Trim(),
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = _currentUserService.UserId
            };
            _context.Customers.Add(customer);
        }

        var invNumber = await _tagGenerator.GenerateInvoiceNumberAsync(shift.Branch?.Code ?? "BR", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var cashierId = _currentUserService.UserId ?? shift.CashierId;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invNumber,
            BranchId = shift.BranchId,
            ShiftId = shift.Id,
            CashierId = cashierId,
            CustomerId = customer.Id,
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PrimaryPhone,
            Status = InvoiceStatus.Draft,
            SubTotal = 0m,
            DiscountAmount = 0m,
            VatRate = 0m,
            VatAmount = 0m,
            TotalAmount = 0m,
            CurrencyCode = shift.CurrencyCode,
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            CreatedBy = cashierId
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetInvoiceByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<PosInvoiceDto> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Branch)
            .Include(i => i.Shift)
            .Include(i => i.Cashier)
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Invoice", invoiceId);

        return MapToInvoiceDto(invoice);
    }

    public async Task<PagedResult<PosInvoiceDto>> GetInvoicesAsync(PosInvoiceFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Invoices
            .Include(i => i.Branch)
            .Include(i => i.Shift)
            .Include(i => i.Cashier)
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Where(i => !i.IsDeleted);

        if (filter.BranchId.HasValue)
        {
            query = query.Where(i => i.BranchId == filter.BranchId.Value);
        }

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(i => i.CustomerId == filter.CustomerId.Value);
        }

        if (filter.CashierId.HasValue)
        {
            query = query.Where(i => i.CashierId == filter.CashierId.Value);
        }

        if (filter.ShiftId.HasValue)
        {
            query = query.Where(i => i.ShiftId == filter.ShiftId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(i => i.Status == filter.Status.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(i => i.CreatedAt <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PosInvoiceDto>(
            items.Select(MapToInvoiceDto).ToList(),
            totalCount,
            page,
            pageSize
        );
    }

    public async Task<PosInvoiceDto> AddLineToDraftAsync(Guid invoiceId, AddPosInvoiceLineRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Shift)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException($"Invoice '{invoice.InvoiceNumber}' is {invoice.Status} and lines can no longer be modified.");
        }

        var lookup = await LookupItemForSaleAsync(invoice.ShiftId, request.ItemBarcodeOrTag, cancellationToken);

        if (invoice.Lines.Any(l => l.InventoryItemId == lookup.InventoryItemId && !l.IsDeleted))
        {
            throw new BusinessRuleException($"Item '{lookup.TagNumber}' has already been added to this invoice.");
        }

        var unitPrice = request.NegotiatedUnitPrice ?? lookup.ReferencePrice;
        if (unitPrice < 0m)
        {
            throw new BusinessRuleException("Negotiated price cannot be negative.");
        }

        var line = new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            LineType = InvoiceLineType.InventoryItem,
            InventoryItemId = lookup.InventoryItemId,
            ItemCode = lookup.ItemCode,
            TagNumber = lookup.TagNumber,
            Barcode = lookup.Barcode,
            ItemName = lookup.ItemName,
            GrossWeight = lookup.GrossWeight,
            StoneWeight = lookup.StoneWeight,
            NetWeight = lookup.NetWeight,
            PieceCount = lookup.PieceCount,
            MaterialId = lookup.MaterialId,
            MaterialNameSnapshot = lookup.MaterialName,
            PurityId = lookup.PurityId,
            KaratValueSnapshot = lookup.KaratValue,
            ReferenceRatePerGram = lookup.ActiveRatePerGram,
            ReferenceMaterialValue = lookup.ReferenceMaterialValue,
            ReferenceMakingCharge = lookup.ReferenceMakingCharge,
            ReferencePrice = lookup.ReferencePrice,
            UnitPrice = unitPrice,
            Quantity = 1,
            LineTotal = unitPrice * 1,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.InvoiceLines.Add(line);
        if (!invoice.Lines.Contains(line))
        {
            invoice.Lines.Add(line);
        }
        RecalculateTotals(invoice);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetInvoiceByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<PosInvoiceDto> UpdateLineInDraftAsync(Guid invoiceId, Guid lineId, UpdatePosInvoiceLineRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException($"Invoice '{invoice.InvoiceNumber}' is {invoice.Status} and lines cannot be modified.");
        }

        var line = invoice.Lines.FirstOrDefault(l => l.Id == lineId && !l.IsDeleted)
            ?? throw new NotFoundException("InvoiceLine", lineId);

        if (request.NegotiatedUnitPrice < 0m)
        {
            throw new BusinessRuleException("Negotiated price cannot be negative.");
        }

        line.UnitPrice = request.NegotiatedUnitPrice;
        line.LineTotal = line.UnitPrice * line.Quantity;
        line.Notes = request.Notes?.Trim();
        line.UpdatedAt = DateTimeOffset.UtcNow;
        line.UpdatedBy = _currentUserService.UserId;

        RecalculateTotals(invoice);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetInvoiceByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<PosInvoiceDto> RemoveLineFromDraftAsync(Guid invoiceId, Guid lineId, CancellationToken cancellationToken = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException($"Invoice '{invoice.InvoiceNumber}' is {invoice.Status} and lines cannot be modified.");
        }

        var line = invoice.Lines.FirstOrDefault(l => l.Id == lineId && !l.IsDeleted)
            ?? throw new NotFoundException("InvoiceLine", lineId);

        _context.InvoiceLines.Remove(line);
        RecalculateTotals(invoice);

        await _context.SaveChangesAsync(cancellationToken);

        return await GetInvoiceByIdAsync(invoice.Id, cancellationToken);
    }

    public async Task<PosInvoiceDto> FinalizeInvoiceAsync(Guid invoiceId, FinalizePosInvoiceRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BusinessRuleException("An 'Idempotency-Key' request header is required to finalize an invoice.");
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var normalizedKey = idempotencyKey.Trim();

        // 1. Idempotency Check (Fast Path)
        var payloadJson = JsonSerializer.Serialize(new { invoiceId, request });
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));

        var existingRecord = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<PosInvoiceDto>(existingRecord.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        // 2. Begin Caller-Owned Outer Transaction
        await using var dbTx = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Under-lock idempotency check
        var underLockRecord = await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

        if (underLockRecord != null)
        {
            if (underLockRecord.RequestHash != requestHash)
            {
                throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
            }

            var cachedDto = JsonSerializer.Deserialize<PosInvoiceDto>(underLockRecord.ResponseBody);
            if (cachedDto != null)
            {
                return cachedDto;
            }
        }

        var invoice = await _context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Include(i => i.Branch)
            .Include(i => i.Shift)
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException($"Invoice '{invoice.InvoiceNumber}' is already {invoice.Status} and cannot be finalized again.");
        }

        if (invoice.Lines.Count == 0)
        {
            throw new BusinessRuleException("Cannot finalize an invoice with no items.");
        }

        if (invoice.Shift == null || invoice.Shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException("Cannot finalize sale: The associated cashier shift is closed.");
        }

        // Apply discount if provided
        if (request.DiscountAmount.HasValue)
        {
            if (request.DiscountAmount.Value < 0m)
            {
                throw new BusinessRuleException("Discount amount cannot be negative.");
            }
            invoice.DiscountAmount = request.DiscountAmount.Value;
        }

        if (request.Notes != null)
        {
            invoice.Notes = request.Notes.Trim();
        }

        // 3. VAT Handling (Section 5 confirmed business rule)
        var vatConfig = await _vatService.GetActiveVatConfigurationAsync(invoice.BranchId, cancellationToken);
        if (vatConfig != null && vatConfig.IsEnabled && vatConfig.RatePercentage > 0)
        {
            // Pending client confirmation on inclusive vs exclusive
            throw new BusinessRuleException("VAT calculation treatment (inclusive vs exclusive) is pending confirmation. Sales can currently only proceed with VAT disabled in branch configuration.");
        }

        invoice.VatRate = 0m;
        invoice.VatAmount = 0m;
        RecalculateTotals(invoice);

        // 4. Validate Payments
        if (request.Payments == null || request.Payments.Count == 0)
        {
            throw new BusinessRuleException("At least one payment must be provided to finalize the sale.");
        }

        var totalPaymentAmount = request.Payments.Sum(p => p.Amount);
        if (totalPaymentAmount != invoice.TotalAmount)
        {
            throw new BusinessRuleException($"Total accepted payment amount ({totalPaymentAmount:N2} {invoice.CurrencyCode}) does not match invoice total ({invoice.TotalAmount:N2} {invoice.CurrencyCode}). Sales must be fully settled.");
        }

        // 5. Concurrency & Inventory Locking (Deterministic order)
        var physicalItemIds = invoice.Lines
            .Where(l => l.InventoryItemId.HasValue)
            .Select(l => l.InventoryItemId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        foreach (var itemId in physicalItemIds)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"InventoryItems\" WHERE \"Id\" = {itemId} FOR UPDATE", cancellationToken);
        }

        var physicalItems = await _context.InventoryItems
            .Where(i => physicalItemIds.Contains(i.Id) && !i.IsDeleted)
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var cashierId = _currentUserService.UserId ?? invoice.CashierId;

        foreach (var line in invoice.Lines)
        {
            if (!line.InventoryItemId.HasValue)
            {
                continue;
            }

            if (!physicalItems.TryGetValue(line.InventoryItemId.Value, out var item))
            {
                throw new NotFoundException("InventoryItem", line.InventoryItemId.Value);
            }

            if (item.Status != InventoryStatus.Available)
            {
                throw new ConflictException($"Item '{item.TagNumber}' is no longer available for sale (current status: {item.Status}).");
            }

            if (item.BranchId != invoice.BranchId)
            {
                throw new ConflictException($"Item '{item.TagNumber}' has been moved to another branch.");
            }

            // Transition item status to Sold
            item.Status = InventoryStatus.Sold;
            item.UpdatedAt = now;
            item.UpdatedBy = cashierId;

            // Handle composition detachment if member of an assembly/group
            await _compositionService.DetachMemberOnSaleAsync(item.Id, invoice.InvoiceNumber, cashierId, autoSave: false, cancellationToken);
        }

        // 6. Process Payments & Cash Ledger Integration
        decimal totalCashTendered = 0m;
        decimal totalCashChange = 0m;
        decimal netCashPaid = 0m;

        foreach (var p in request.Payments)
        {
            if (p.Amount <= 0m)
            {
                throw new BusinessRuleException("Payment amount must be greater than zero.");
            }

            Guid? cashLedgerTxId = null;
            decimal? cashTendered = null;
            decimal? changeGiven = null;

            if (p.Method == PaymentMethod.Cash)
            {
                cashTendered = p.CashTendered ?? p.Amount;
                if (cashTendered.Value < p.Amount)
                {
                    throw new BusinessRuleException($"Cash tendered ({cashTendered.Value:N2}) cannot be less than payment amount ({p.Amount:N2}).");
                }
                changeGiven = cashTendered.Value - p.Amount;

                totalCashTendered += cashTendered.Value;
                totalCashChange += changeGiven.Value;
                netCashPaid += p.Amount;

                // Post Cash In to Cash Ledger (Participating in the ambient transaction, autoSave: false)
                var cashTx = await _cashLedgerService.PostCashPaymentAsync(
                    CashLedgerSourceType.PosSale,
                    invoice.InvoiceNumber,
                    invoice.ShiftId,
                    p.Amount,
                    invoice.CurrencyCode,
                    $"POS Cash Sale {invoice.InvoiceNumber}",
                    autoSave: false,
                    cancellationToken
                );

                cashLedgerTxId = cashTx.Id;
            }
            else if (p.Method == PaymentMethod.Card)
            {
                if (string.IsNullOrWhiteSpace(p.ReferenceNumber) && string.IsNullOrWhiteSpace(p.CardLast4))
                {
                    throw new BusinessRuleException("Card payments require an approval/reference number or last 4 digits.");
                }
            }
            else if (p.Method == PaymentMethod.BankTransfer)
            {
                if (string.IsNullOrWhiteSpace(p.ReferenceNumber))
                {
                    throw new BusinessRuleException("Bank transfer payments require a transaction reference number.");
                }
            }
            else
            {
                throw new BusinessRuleException($"Unsupported payment method: {p.Method}");
            }

            var payment = new InvoicePayment
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                PaymentMethod = p.Method,
                Amount = p.Amount,
                CashTendered = cashTendered,
                ChangeGiven = changeGiven,
                ReferenceNumber = p.ReferenceNumber?.Trim(),
                CardType = p.CardType?.Trim(),
                CardLast4 = p.CardLast4?.Trim(),
                BankName = p.BankName?.Trim(),
                CashLedgerTransactionId = cashLedgerTxId,
                Notes = p.Notes?.Trim(),
                CreatedAt = now,
                CreatedBy = cashierId
            };

            _context.InvoicePayments.Add(payment);
            if (!invoice.Payments.Contains(payment))
            {
                invoice.Payments.Add(payment);
            }
        }

        // 7. Finalize Invoice State
        invoice.Status = InvoiceStatus.Finalized;
        invoice.FinalizedAt = now;
        invoice.CashierId = cashierId;
        invoice.TotalPaidAmount = totalPaymentAmount;
        invoice.TotalCashTendered = totalCashTendered;
        invoice.TotalCashChange = totalCashChange;
        invoice.NetCashPaid = netCashPaid;
        invoice.IdempotencyKey = normalizedKey;
        invoice.UpdatedAt = now;
        invoice.UpdatedBy = cashierId;

        // Record Audit Log
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Action = "INVOICE_FINALIZED",
            UserId = cashierId,
            Username = _currentUserService.Username,
            BranchId = invoice.BranchId,
            NewValues = JsonSerializer.Serialize(new
            {
                invoice.InvoiceNumber,
                invoice.TotalAmount,
                ItemCount = invoice.Lines.Count,
                PaymentCount = invoice.Payments.Count,
                invoice.NetCashPaid
            }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = now
        });

        var resultDto = MapToInvoiceDto(invoice);

        // Record Idempotency
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = normalizedKey,
            Endpoint = $"/api/pos/invoices/{invoiceId}/finalize",
            RequestHash = requestHash,
            ResponseBody = JsonSerializer.Serialize(resultDto),
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await dbTx.RollbackAsync(cancellationToken);

            var committed = await _context.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == normalizedKey, cancellationToken);

            if (committed != null)
            {
                var cached = JsonSerializer.Deserialize<PosInvoiceDto>(committed.ResponseBody);
                if (cached != null)
                {
                    return cached;
                }
            }

            throw;
        }

        _logger.LogInformation("Finalized Invoice {InvoiceNumber} for {Total:N2} {Currency} by Cashier {CashierId}",
            invoice.InvoiceNumber, invoice.TotalAmount, invoice.CurrencyCode, cashierId);

        return resultDto;
    }

    public async Task<PosInvoiceDto> CancelDraftInvoiceAsync(Guid invoiceId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("A cancellation reason is required.");
        }

        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Invoice", invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException($"Only Draft invoices can be cancelled. Current status is {invoice.Status}.");
        }

        var now = DateTimeOffset.UtcNow;
        invoice.Status = InvoiceStatus.Cancelled;
        invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
            ? $"Cancelled: {reason.Trim()}"
            : $"{invoice.Notes} | Cancelled: {reason.Trim()}";
        invoice.UpdatedAt = now;
        invoice.UpdatedBy = _currentUserService.UserId;

        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(Invoice),
            EntityId = invoice.Id.ToString(),
            Action = "INVOICE_CANCELLED",
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = invoice.BranchId,
            NewValues = JsonSerializer.Serialize(new { invoice.InvoiceNumber, Reason = reason }),
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return await GetInvoiceByIdAsync(invoice.Id, cancellationToken);
    }

    private static void RecalculateTotals(Invoice invoice)
    {
        invoice.SubTotal = invoice.Lines.Where(l => !l.IsDeleted).Sum(l => l.LineTotal);
        var afterDiscount = invoice.SubTotal - invoice.DiscountAmount;
        if (afterDiscount < 0m)
        {
            afterDiscount = 0m;
        }

        invoice.TotalAmount = afterDiscount + invoice.VatAmount;
    }

    private static PosInvoiceDto MapToInvoiceDto(Invoice i)
    {
        var lineDtos = i.Lines.Where(l => !l.IsDeleted).Select(l => new PosInvoiceLineDto(
            l.Id,
            (int)l.LineType,
            l.LineType.ToString(),
            l.InventoryItemId,
            l.CompositionId,
            l.ItemCode,
            l.TagNumber,
            l.Barcode,
            l.ItemName,
            l.GrossWeight,
            l.StoneWeight,
            l.NetWeight,
            l.PieceCount,
            l.MaterialNameSnapshot,
            l.KaratValueSnapshot,
            l.ReferenceRatePerGram,
            l.ReferenceMaterialValue,
            l.ReferenceMakingCharge,
            l.ReferencePrice,
            l.UnitPrice,
            l.Quantity,
            l.LineTotal,
            l.Notes
        )).ToList();

        var paymentDtos = i.Payments.Where(p => !p.IsDeleted).Select(p => new PosInvoicePaymentDto(
            p.Id,
            (int)p.PaymentMethod,
            p.PaymentMethod.ToString(),
            p.Amount,
            p.CashTendered,
            p.ChangeGiven,
            p.ReferenceNumber,
            p.CardType,
            p.CardLast4,
            p.BankName,
            p.CashLedgerTransactionId,
            p.Notes,
            p.CreatedAt
        )).ToList();

        return new PosInvoiceDto(
            i.Id,
            i.InvoiceNumber,
            i.BranchId,
            i.Branch?.Name,
            i.ShiftId,
            i.Shift?.ShiftNumber,
            i.CashierId,
            i.Cashier?.FullName ?? i.Cashier?.Username,
            i.CustomerId,
            i.Customer?.CustomerCode,
            i.CustomerNameSnapshot,
            i.CustomerPhoneSnapshot,
            (int)i.Status,
            i.Status.ToString(),
            i.SubTotal,
            i.DiscountAmount,
            i.VatRate,
            i.VatAmount,
            i.TotalAmount,
            i.TotalPaidAmount,
            i.TotalCashTendered,
            i.TotalCashChange,
            i.NetCashPaid,
            i.CurrencyCode,
            i.Notes,
            i.CreatedAt,
            i.FinalizedAt,
            lineDtos,
            paymentDtos
        );
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Customers;
using Saranga.Domain.Common;
using Saranga.Domain.Entities;

namespace Saranga.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly IAppDbContext _context;
    private readonly ITagGeneratorService _tagGeneratorService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPhoneNormalizationService _phoneNormalizer;
    private readonly ILogger<CustomerService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CustomerService(
        IAppDbContext context,
        ITagGeneratorService tagGeneratorService,
        ICurrentUserService currentUserService,
        IPhoneNormalizationService phoneNormalizer,
        ILogger<CustomerService> logger)
    {
        _context = context;
        _tagGeneratorService = tagGeneratorService;
        _currentUserService = currentUserService;
        _phoneNormalizer = phoneNormalizer;
        _logger = logger;
    }

    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerQueryParams queryParams, CancellationToken cancellationToken = default)
    {
        var query = _context.Customers
            .AsNoTracking()
            .Include(c => c.Creator)
            .Include(c => c.Updater)
            .AsQueryable();

        if (queryParams.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == queryParams.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var term = queryParams.Search.Trim();
            var digitsOnly = _phoneNormalizer.ExtractDigits(term);
            var normalizedFull = _phoneNormalizer.Normalize(term);
            var termLower = term.ToLower();

            query = query.Where(c =>
                c.CustomerCode.ToLower().Contains(termLower) ||
                c.FullName.ToLower().Contains(termLower) ||
                (c.NormalizedEmail != null && c.NormalizedEmail.Contains(termLower)) ||
                c.PrimaryPhone.Contains(term) ||
                (digitsOnly.Length >= 3 && c.NormalizedPrimaryPhone.Contains(digitsOnly)) ||
                (normalizedFull != null && c.NormalizedPrimaryPhone.Contains(normalizedFull)) ||
                (c.NormalizedSecondaryPhone != null && digitsOnly.Length >= 3 && c.NormalizedSecondaryPhone.Contains(digitsOnly)) ||
                (c.NormalizedSecondaryPhone != null && normalizedFull != null && c.NormalizedSecondaryPhone.Contains(normalizedFull)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = queryParams.Page < 1 ? 1 : queryParams.Page;
        var pageSize = queryParams.PageSize is < 1 or > 200 ? 25 : queryParams.PageSize;

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<CustomerDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<List<CustomerLookupDto>> SearchCustomersForLookupAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return new List<CustomerLookupDto>();
        }

        var term = query.Trim();
        var digitsOnly = _phoneNormalizer.ExtractDigits(term);
        var normalizedFull = _phoneNormalizer.Normalize(term);
        var termLower = term.ToLower();

        var customers = await _context.Customers
            .AsNoTracking()
            .Where(c => c.IsActive && (
                c.CustomerCode.ToLower().Contains(termLower) ||
                c.FullName.ToLower().Contains(termLower) ||
                (c.NormalizedEmail != null && c.NormalizedEmail.Contains(termLower)) ||
                c.PrimaryPhone.Contains(term) ||
                (digitsOnly.Length >= 3 && c.NormalizedPrimaryPhone.Contains(digitsOnly)) ||
                (normalizedFull != null && c.NormalizedPrimaryPhone.Contains(normalizedFull))))
            .OrderBy(c => c.FullName)
            .Take(maxResults is < 1 or > 50 ? 20 : maxResults)
            .Select(c => new CustomerLookupDto(
                c.Id,
                c.CustomerCode,
                c.FullName,
                c.PrimaryPhone,
                c.Email,
                c.City,
                c.IsActive
            ))
            .ToListAsync(cancellationToken);

        return customers;
    }

    public async Task<CustomerDto> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.Creator)
            .Include(c => c.Updater)
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer", customerId);

        return MapToDto(customer);
    }

    public async Task<CustomerDto> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        var trimmedCode = customerCode.Trim();
        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.Creator)
            .Include(c => c.Updater)
            .FirstOrDefaultAsync(c => c.CustomerCode.ToUpper() == trimmedCode.ToUpper(), cancellationToken)
            ?? throw new NotFoundException($"Customer with code '{customerCode}' was not found.");

        return MapToDto(customer);
    }

    public async Task<CustomerDuplicateCheckResult> CheckDuplicateAsync(string primaryPhone, string? email = null, Guid? excludeCustomerId = null, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = _phoneNormalizer.Normalize(primaryPhone);
        if (!string.IsNullOrEmpty(normalizedPhone))
        {
            var phoneMatch = await _context.Customers
                .AsNoTracking()
                .Where(c => c.NormalizedPrimaryPhone == normalizedPhone && (!excludeCustomerId.HasValue || c.Id != excludeCustomerId.Value))
                .FirstOrDefaultAsync(cancellationToken);

            if (phoneMatch != null)
            {
                return new CustomerDuplicateCheckResult(
                    IsDuplicate: true,
                    DuplicateReason: $"A customer record already exists with primary phone number '{phoneMatch.PrimaryPhone}' (Code: {phoneMatch.CustomerCode}, Name: {phoneMatch.FullName}).",
                    ExistingCustomer: new CustomerLookupDto(phoneMatch.Id, phoneMatch.CustomerCode, phoneMatch.FullName, phoneMatch.PrimaryPhone, phoneMatch.Email, phoneMatch.City, phoneMatch.IsActive)
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var cleanEmail = email.Trim().ToLowerInvariant();
            var emailMatch = await _context.Customers
                .AsNoTracking()
                .Where(c => c.NormalizedEmail == cleanEmail && (!excludeCustomerId.HasValue || c.Id != excludeCustomerId.Value))
                .FirstOrDefaultAsync(cancellationToken);

            if (emailMatch != null)
            {
                return new CustomerDuplicateCheckResult(
                    IsDuplicate: true,
                    DuplicateReason: $"A customer record already exists with email address '{emailMatch.Email}' (Code: {emailMatch.CustomerCode}, Name: {emailMatch.FullName}).",
                    ExistingCustomer: new CustomerLookupDto(emailMatch.Id, emailMatch.CustomerCode, emailMatch.FullName, emailMatch.PrimaryPhone, emailMatch.Email, emailMatch.City, emailMatch.IsActive)
                );
            }
        }

        return new CustomerDuplicateCheckResult(false, null, null);
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new BusinessRuleException("Customer full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryPhone))
        {
            throw new BusinessRuleException("Primary phone number is required.");
        }

        var normalizedPhone = _phoneNormalizer.Normalize(request.PrimaryPhone);
        if (string.IsNullOrEmpty(normalizedPhone) || normalizedPhone.Length < 7)
        {
            throw new BusinessRuleException("Primary phone number must contain at least 7 digits.");
        }

        string? cleanEmail = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            cleanEmail = request.Email.Trim().ToLowerInvariant();
            if (!cleanEmail.Contains('@') || !cleanEmail.Contains('.'))
            {
                throw new BusinessRuleException($"Email address '{request.Email}' is not in a valid format.");
            }
        }

        // Duplicate Check
        var dupCheck = await CheckDuplicateAsync(request.PrimaryPhone, cleanEmail, null, cancellationToken);
        if (dupCheck.IsDuplicate)
        {
            throw new BusinessRuleException(dupCheck.DuplicateReason!);
        }

        string? normalizedSecPhone = null;
        if (!string.IsNullOrWhiteSpace(request.SecondaryPhone))
        {
            normalizedSecPhone = _phoneNormalizer.Normalize(request.SecondaryPhone);
        }

        // Generate Customer Code from database sequence via TagGeneratorService
        var customerCode = await _tagGeneratorService.GenerateCustomerCodeAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var customer = new Customer
        {
            CustomerCode = customerCode,
            FullName = request.FullName.Trim(),
            PrimaryPhone = request.PrimaryPhone.Trim(),
            NormalizedPrimaryPhone = normalizedPhone,
            SecondaryPhone = string.IsNullOrWhiteSpace(request.SecondaryPhone) ? null : request.SecondaryPhone.Trim(),
            NormalizedSecondaryPhone = string.IsNullOrEmpty(normalizedSecPhone) ? null : normalizedSecPhone,
            Email = cleanEmail,
            NormalizedEmail = cleanEmail,
            AddressLine1 = string.IsNullOrWhiteSpace(request.AddressLine1) ? null : request.AddressLine1.Trim(),
            AddressLine2 = string.IsNullOrWhiteSpace(request.AddressLine2) ? null : request.AddressLine2.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            DistrictProvince = string.IsNullOrWhiteSpace(request.DistrictProvince) ? null : request.DistrictProvince.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim(),
            Country = string.IsNullOrWhiteSpace(request.Country) ? "Sri Lanka" : request.Country.Trim(),
            DateOfBirth = request.DateOfBirth,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsActive = true,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserId
        };

        _context.Customers.Add(customer);

        RecordAudit("Customer", customer.Id, "CUSTOMER_CREATED", null, new
        {
            customer.CustomerCode,
            customer.FullName,
            customer.PrimaryPhone,
            customer.Email
        });

        await SaveChangesWithDuplicateHandlingAsync(cancellationToken);

        return await GetCustomerByIdAsync(customer.Id, cancellationToken);
    }

    public async Task<CustomerDto> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer", customerId);

        // Optimistic Concurrency Token Verification
        if (request.RowVersion.HasValue && customer.Version != request.RowVersion.Value)
        {
            throw new ConflictException("This customer record was modified concurrently by another user. Please refresh and review latest details before retrying.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new BusinessRuleException("Customer full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PrimaryPhone))
        {
            throw new BusinessRuleException("Primary phone number is required.");
        }

        var normalizedPhone = _phoneNormalizer.Normalize(request.PrimaryPhone);
        if (string.IsNullOrEmpty(normalizedPhone) || normalizedPhone.Length < 7)
        {
            throw new BusinessRuleException("Primary phone number must contain at least 7 digits.");
        }

        string? cleanEmail = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            cleanEmail = request.Email.Trim().ToLowerInvariant();
            if (!cleanEmail.Contains('@') || !cleanEmail.Contains('.'))
            {
                throw new BusinessRuleException($"Email address '{request.Email}' is not in a valid format.");
            }
        }

        // Duplicate Check excluding this customer
        var dupCheck = await CheckDuplicateAsync(request.PrimaryPhone, cleanEmail, customer.Id, cancellationToken);
        if (dupCheck.IsDuplicate)
        {
            throw new BusinessRuleException(dupCheck.DuplicateReason!);
        }

        string? normalizedSecPhone = null;
        if (!string.IsNullOrWhiteSpace(request.SecondaryPhone))
        {
            normalizedSecPhone = _phoneNormalizer.Normalize(request.SecondaryPhone);
        }

        var oldState = new
        {
            customer.FullName,
            customer.PrimaryPhone,
            customer.Email,
            customer.City
        };

        // CustomerCode is immutable!
        customer.FullName = request.FullName.Trim();
        customer.PrimaryPhone = request.PrimaryPhone.Trim();
        customer.NormalizedPrimaryPhone = normalizedPhone;
        customer.SecondaryPhone = string.IsNullOrWhiteSpace(request.SecondaryPhone) ? null : request.SecondaryPhone.Trim();
        customer.NormalizedSecondaryPhone = string.IsNullOrEmpty(normalizedSecPhone) ? null : normalizedSecPhone;
        customer.Email = cleanEmail;
        customer.NormalizedEmail = cleanEmail;
        customer.AddressLine1 = string.IsNullOrWhiteSpace(request.AddressLine1) ? null : request.AddressLine1.Trim();
        customer.AddressLine2 = string.IsNullOrWhiteSpace(request.AddressLine2) ? null : request.AddressLine2.Trim();
        customer.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        customer.DistrictProvince = string.IsNullOrWhiteSpace(request.DistrictProvince) ? null : request.DistrictProvince.Trim();
        customer.PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim();
        customer.Country = string.IsNullOrWhiteSpace(request.Country) ? "Sri Lanka" : request.Country.Trim();
        customer.DateOfBirth = request.DateOfBirth;
        customer.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        customer.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Customer", customer.Id, "CUSTOMER_UPDATED", oldState, new
        {
            customer.FullName,
            customer.PrimaryPhone,
            customer.Email,
            customer.City
        });

        await SaveChangesWithDuplicateHandlingAsync(cancellationToken);

        return await GetCustomerByIdAsync(customer.Id, cancellationToken);
    }

    public async Task<CustomerDto> UpdateCustomerStatusAsync(Guid customerId, UpdateCustomerStatusRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer", customerId);

        var oldStatus = customer.IsActive;
        customer.IsActive = request.IsActive;
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        customer.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Customer", customer.Id, "CUSTOMER_STATUS_CHANGED",
            new { IsActive = oldStatus },
            new { IsActive = customer.IsActive, Reason = request.Reason });

        await SaveChangesWithDuplicateHandlingAsync(cancellationToken);

        return await GetCustomerByIdAsync(customer.Id, cancellationToken);
    }

    private async Task SaveChangesWithDuplicateHandlingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            if (msg.Contains("NormalizedPrimaryPhone", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("IX_Customers_NormalizedPrimaryPhone", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("A customer record already exists with this phone number.");
            }
            if (msg.Contains("NormalizedEmail", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("IX_Customers_NormalizedEmail", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("A customer record already exists with this email address.");
            }
            if (msg.Contains("23505") || msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("A customer record already exists with matching unique details.");
            }
            throw;
        }
    }

    private static CustomerDto MapToDto(Customer c)
    {
        return new CustomerDto(
            c.Id,
            c.CustomerCode,
            c.FullName,
            c.PrimaryPhone,
            c.NormalizedPrimaryPhone,
            c.SecondaryPhone,
            c.NormalizedSecondaryPhone,
            c.Email,
            c.AddressLine1,
            c.AddressLine2,
            c.City,
            c.DistrictProvince,
            c.PostalCode,
            c.Country,
            c.DateOfBirth,
            c.Notes,
            c.IsActive,
            c.Version,
            c.CreatedAt,
            c.CreatedBy,
            c.Creator?.FullName,
            c.UpdatedAt,
            c.UpdatedBy,
            c.Updater?.FullName
        );
    }

    private void RecordAudit(string entityName, Guid entityId, string action, object? oldValues, object? newValues)
    {
        var audit = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Action = action,
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = _currentUserService.BranchId,
            Timestamp = DateTimeOffset.UtcNow,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent
        };

        _context.AuditLogs.Add(audit);
    }
}

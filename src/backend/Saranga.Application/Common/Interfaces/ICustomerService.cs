using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Customers;

namespace Saranga.Application.Common.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerQueryParams queryParams, CancellationToken cancellationToken = default);
    Task<List<CustomerLookupDto>> SearchCustomersForLookupAsync(string query, int maxResults = 20, CancellationToken cancellationToken = default);
    Task<CustomerDto> GetCustomerByIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<CustomerDto> GetCustomerByCodeAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<CustomerDuplicateCheckResult> CheckDuplicateAsync(string primaryPhone, string? email = null, Guid? excludeCustomerId = null, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerDto> UpdateCustomerStatusAsync(Guid customerId, UpdateCustomerStatusRequest request, CancellationToken cancellationToken = default);
}

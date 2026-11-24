using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Customers;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    [RequirePermission(Permissions.CustomerView)]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerDto>>> GetCustomers(
        [FromQuery] CustomerQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        var result = await _customerService.GetCustomersAsync(queryParams, cancellationToken);
        return Ok(result);
    }

    [HttpGet("search")]
    [RequirePermission(Permissions.CustomerView)]
    [ProducesResponseType(typeof(List<CustomerLookupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CustomerLookupDto>>> SearchCustomers(
        [FromQuery] string q,
        [FromQuery] int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _customerService.SearchCustomersForLookupAsync(q, maxResults, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CustomerView)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetCustomerById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _customerService.GetCustomerByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("code/{customerCode}")]
    [RequirePermission(Permissions.CustomerView)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetCustomerByCode(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var result = await _customerService.GetCustomerByCodeAsync(customerCode, cancellationToken);
        return Ok(result);
    }

    [HttpGet("check-duplicate")]
    [RequirePermission(Permissions.CustomerView)]
    [ProducesResponseType(typeof(CustomerDuplicateCheckResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDuplicateCheckResult>> CheckDuplicate(
        [FromQuery] string primaryPhone,
        [FromQuery] string? email,
        [FromQuery] Guid? excludeCustomerId,
        CancellationToken cancellationToken)
    {
        var result = await _customerService.CheckDuplicateAsync(primaryPhone, email, excludeCustomerId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.CustomerCreate, Permissions.CustomerManage)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _customerService.CreateCustomerAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CustomerEdit, Permissions.CustomerManage)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> UpdateCustomer(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _customerService.UpdateCustomerAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.CustomerActivateDeactivate, Permissions.CustomerManage)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> UpdateCustomerStatus(
        Guid id,
        [FromBody] UpdateCustomerStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _customerService.UpdateCustomerStatusAsync(id, request, cancellationToken);
        return Ok(result);
    }
}

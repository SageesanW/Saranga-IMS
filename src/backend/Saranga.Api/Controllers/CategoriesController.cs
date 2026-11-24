using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Attributes;
using Saranga.Application.Services;
using Saranga.Domain.Constants;
using Saranga.Infrastructure.Authorization;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly IAttributesService _attributesService;

    public CategoriesController(IAttributesService attributesService)
    {
        _attributesService = attributesService;
    }

    [HttpGet]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories([FromQuery] bool? isActive, CancellationToken cancellationToken)
    {
        var list = await _attributesService.GetCategoriesAsync(isActive, cancellationToken);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AttributesView)]
    public async Task<ActionResult<CategoryDto>> GetCategoryById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.GetCategoryByIdAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost]
    [RequirePermission(Permissions.AttributesCreate)]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.CreateCategoryAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCategoryById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AttributesEdit)]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var item = await _attributesService.UpdateCategoryAsync(id, request, cancellationToken);
        return Ok(item);
    }

    [HttpPatch("{id:guid}/status")]
    [RequirePermission(Permissions.AttributesActivateDeactivate)]
    public async Task<ActionResult<CategoryDto>> ToggleCategoryStatus(Guid id, CancellationToken cancellationToken)
    {
        var item = await _attributesService.ToggleCategoryStatusAsync(id, cancellationToken);
        return Ok(item);
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Attributes;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public interface IAttributesService
{
    // Materials
    Task<List<MaterialDto>> GetMaterialsAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<MaterialDto> GetMaterialByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request, CancellationToken cancellationToken = default);
    Task<MaterialDto> UpdateMaterialAsync(Guid id, UpdateMaterialRequest request, CancellationToken cancellationToken = default);
    Task<MaterialDto> ToggleMaterialStatusAsync(Guid id, CancellationToken cancellationToken = default);

    // Categories
    Task<List<CategoryDto>> GetCategoriesAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<CategoryDto> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> ToggleCategoryStatusAsync(Guid id, CancellationToken cancellationToken = default);

    // Purities
    Task<List<PurityDto>> GetPuritiesAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<PurityDto> GetPurityByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurityDto> CreatePurityAsync(CreatePurityRequest request, CancellationToken cancellationToken = default);
    Task<PurityDto> UpdatePurityAsync(Guid id, UpdatePurityRequest request, CancellationToken cancellationToken = default);
    Task<PurityDto> TogglePurityStatusAsync(Guid id, CancellationToken cancellationToken = default);

    // Specifications
    Task<List<SpecificationDto>> GetSpecificationsAsync(bool? isActive = null, CancellationToken cancellationToken = default);
    Task<SpecificationDto> GetSpecificationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SpecificationDto> CreateSpecificationAsync(CreateSpecificationRequest request, CancellationToken cancellationToken = default);
    Task<SpecificationDto> UpdateSpecificationAsync(Guid id, UpdateSpecificationRequest request, CancellationToken cancellationToken = default);
    Task<SpecificationDto> ToggleSpecificationStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SpecificationOptionDto> AddSpecificationOptionAsync(Guid specificationId, CreateSpecificationOptionRequest request, CancellationToken cancellationToken = default);
    Task<SpecificationOptionDto> ToggleSpecificationOptionStatusAsync(Guid specificationId, Guid optionId, CancellationToken cancellationToken = default);

    // Stock / Display Locations (Branch-scoped)
    Task<List<StockLocationDto>> GetStockLocationsAsync(Guid? branchId = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<StockLocationDto> GetStockLocationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StockLocationDto> CreateStockLocationAsync(CreateStockLocationRequest request, CancellationToken cancellationToken = default);
    Task<StockLocationDto> UpdateStockLocationAsync(Guid id, UpdateStockLocationRequest request, CancellationToken cancellationToken = default);
    Task<StockLocationDto> ToggleStockLocationStatusAsync(Guid id, CancellationToken cancellationToken = default);

    // Making Charge Methods
    List<MakingChargeMethodDto> GetMakingChargeMethods();
}

public class AttributesService : IAttributesService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchAuthorizationService _branchAuthService;

    public AttributesService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IBranchAuthorizationService branchAuthService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _branchAuthService = branchAuthService;
    }

    #region Materials
    public async Task<List<MaterialDto>> GetMaterialsAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Materials.AsNoTracking().AsQueryable();
        if (isActive.HasValue) query = query.Where(m => m.IsActive == isActive.Value);

        return await query
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .Select(m => new MaterialDto(m.Id, m.Name, m.Code, m.Description, m.IsActive, m.SortOrder, m.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<MaterialDto> GetMaterialByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _context.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m == null) throw new NotFoundException("Material", id);
        return new MaterialDto(m.Id, m.Name, m.Code, m.Description, m.IsActive, m.SortOrder, m.CreatedAt);
    }

    public async Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Material name cannot be empty.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Material code cannot be empty.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.Materials.AnyAsync(m => m.Code.ToUpper() == normalizedCode, cancellationToken);
        if (exists) throw new BusinessRuleException($"A material with code '{request.Code}' already exists.");

        var material = new Material
        {
            Name = request.Name.Trim(),
            Code = normalizedCode,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.Materials.Add(material);
        RecordAudit("Material", material.Id, "CREATE", null, material);
        await _context.SaveChangesAsync(cancellationToken);

        return new MaterialDto(material.Id, material.Name, material.Code, material.Description, material.IsActive, material.SortOrder, material.CreatedAt);
    }

    public async Task<MaterialDto> UpdateMaterialAsync(Guid id, UpdateMaterialRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Material name cannot be empty.");

        var m = await _context.Materials.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m == null) throw new NotFoundException("Material", id);

        var oldValues = new { m.Name, m.Description, m.IsActive, m.SortOrder };

        m.Name = request.Name.Trim();
        m.Description = request.Description?.Trim() ?? string.Empty;
        m.IsActive = request.IsActive;
        m.SortOrder = request.SortOrder;
        m.UpdatedAt = DateTimeOffset.UtcNow;
        m.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Material", m.Id, "UPDATE", oldValues, m);
        await _context.SaveChangesAsync(cancellationToken);

        return new MaterialDto(m.Id, m.Name, m.Code, m.Description, m.IsActive, m.SortOrder, m.CreatedAt);
    }

    public async Task<MaterialDto> ToggleMaterialStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _context.Materials.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m == null) throw new NotFoundException("Material", id);

        var previousStatus = m.IsActive;
        m.IsActive = !m.IsActive;
        m.UpdatedAt = DateTimeOffset.UtcNow;
        m.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Material", m.Id, m.IsActive ? "ACTIVATED" : "DEACTIVATED", new { IsActive = previousStatus }, new { m.IsActive });
        await _context.SaveChangesAsync(cancellationToken);

        return new MaterialDto(m.Id, m.Name, m.Code, m.Description, m.IsActive, m.SortOrder, m.CreatedAt);
    }
    #endregion

    #region Categories
    public async Task<List<CategoryDto>> GetCategoriesAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Categories.AsNoTracking().AsQueryable();
        if (isActive.HasValue) query = query.Where(c => c.IsActive == isActive.Value);

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Code, c.Description, c.IsActive, c.SortOrder, c.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var c = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c == null) throw new NotFoundException("Category", id);
        return new CategoryDto(c.Id, c.Name, c.Code, c.Description, c.IsActive, c.SortOrder, c.CreatedAt);
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Category name cannot be empty.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Category code cannot be empty.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.Categories.AnyAsync(c => c.Code.ToUpper() == normalizedCode, cancellationToken);
        if (exists) throw new BusinessRuleException($"A category with code '{request.Code}' already exists.");

        var category = new Category
        {
            Name = request.Name.Trim(),
            Code = normalizedCode,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.Categories.Add(category);
        RecordAudit("Category", category.Id, "CREATE", null, category);
        await _context.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, category.Code, category.Description, category.IsActive, category.SortOrder, category.CreatedAt);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Category name cannot be empty.");

        var c = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c == null) throw new NotFoundException("Category", id);

        var oldValues = new { c.Name, c.Description, c.IsActive, c.SortOrder };

        c.Name = request.Name.Trim();
        c.Description = request.Description?.Trim() ?? string.Empty;
        c.IsActive = request.IsActive;
        c.SortOrder = request.SortOrder;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        c.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Category", c.Id, "UPDATE", oldValues, c);
        await _context.SaveChangesAsync(cancellationToken);

        return new CategoryDto(c.Id, c.Name, c.Code, c.Description, c.IsActive, c.SortOrder, c.CreatedAt);
    }

    public async Task<CategoryDto> ToggleCategoryStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var c = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c == null) throw new NotFoundException("Category", id);

        var previousStatus = c.IsActive;
        c.IsActive = !c.IsActive;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        c.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Category", c.Id, c.IsActive ? "ACTIVATED" : "DEACTIVATED", new { IsActive = previousStatus }, new { c.IsActive });
        await _context.SaveChangesAsync(cancellationToken);

        return new CategoryDto(c.Id, c.Name, c.Code, c.Description, c.IsActive, c.SortOrder, c.CreatedAt);
    }
    #endregion

    #region Purities
    public async Task<List<PurityDto>> GetPuritiesAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Purities.AsNoTracking().AsQueryable();
        if (isActive.HasValue) query = query.Where(p => p.IsActive == isActive.Value);

        return await query
            .OrderByDescending(p => p.KaratValue)
            .ThenBy(p => p.SortOrder)
            .Select(p => new PurityDto(p.Id, p.Name, p.Code, p.KaratValue, p.PurityPercentage, p.Description, p.IsActive, p.SortOrder, p.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PurityDto> GetPurityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _context.Purities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p == null) throw new NotFoundException("Purity", id);
        return new PurityDto(p.Id, p.Name, p.Code, p.KaratValue, p.PurityPercentage, p.Description, p.IsActive, p.SortOrder, p.CreatedAt);
    }

    public async Task<PurityDto> CreatePurityAsync(CreatePurityRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Purity name cannot be empty.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Purity code cannot be empty.");
        if (request.KaratValue <= 0 || request.KaratValue > 24) throw new BusinessRuleException("Karat value must be between 1 and 24.");
        if (request.PurityPercentage <= 0 || request.PurityPercentage > 100) throw new BusinessRuleException("Purity percentage must be between 0.01% and 100.00%.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.Purities.AnyAsync(p => p.Code.ToUpper() == normalizedCode, cancellationToken);
        if (exists) throw new BusinessRuleException($"A purity with code '{request.Code}' already exists.");

        var purity = new Purity
        {
            Name = request.Name.Trim(),
            Code = normalizedCode,
            KaratValue = Math.Round(request.KaratValue, 2),
            PurityPercentage = Math.Round(request.PurityPercentage, 2),
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.Purities.Add(purity);
        RecordAudit("Purity", purity.Id, "CREATE", null, purity);
        await _context.SaveChangesAsync(cancellationToken);

        return new PurityDto(purity.Id, purity.Name, purity.Code, purity.KaratValue, purity.PurityPercentage, purity.Description, purity.IsActive, purity.SortOrder, purity.CreatedAt);
    }

    public async Task<PurityDto> UpdatePurityAsync(Guid id, UpdatePurityRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Purity name cannot be empty.");
        if (request.KaratValue <= 0 || request.KaratValue > 24) throw new BusinessRuleException("Karat value must be between 1 and 24.");
        if (request.PurityPercentage <= 0 || request.PurityPercentage > 100) throw new BusinessRuleException("Purity percentage must be between 0.01% and 100.00%.");

        var p = await _context.Purities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p == null) throw new NotFoundException("Purity", id);

        var oldValues = new { p.Name, p.KaratValue, p.PurityPercentage, p.Description, p.IsActive, p.SortOrder };

        p.Name = request.Name.Trim();
        p.KaratValue = Math.Round(request.KaratValue, 2);
        p.PurityPercentage = Math.Round(request.PurityPercentage, 2);
        p.Description = request.Description?.Trim() ?? string.Empty;
        p.IsActive = request.IsActive;
        p.SortOrder = request.SortOrder;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        p.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Purity", p.Id, "UPDATE", oldValues, p);
        await _context.SaveChangesAsync(cancellationToken);

        return new PurityDto(p.Id, p.Name, p.Code, p.KaratValue, p.PurityPercentage, p.Description, p.IsActive, p.SortOrder, p.CreatedAt);
    }

    public async Task<PurityDto> TogglePurityStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _context.Purities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p == null) throw new NotFoundException("Purity", id);

        var previousStatus = p.IsActive;
        p.IsActive = !p.IsActive;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        p.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Purity", p.Id, p.IsActive ? "ACTIVATED" : "DEACTIVATED", new { IsActive = previousStatus }, new { p.IsActive });
        await _context.SaveChangesAsync(cancellationToken);

        return new PurityDto(p.Id, p.Name, p.Code, p.KaratValue, p.PurityPercentage, p.Description, p.IsActive, p.SortOrder, p.CreatedAt);
    }
    #endregion

    #region Specifications
    public async Task<List<SpecificationDto>> GetSpecificationsAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Specifications
            .AsNoTracking()
            .Include(s => s.Options)
            .AsQueryable();

        if (isActive.HasValue) query = query.Where(s => s.IsActive == isActive.Value);

        var list = await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return list.Select(s => new SpecificationDto(
            s.Id,
            s.Name,
            s.Code,
            s.Description,
            s.IsActive,
            s.SortOrder,
            s.Options
                .Where(o => !isActive.HasValue || o.IsActive == isActive.Value)
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Value)
                .Select(o => new SpecificationOptionDto(o.Id, o.SpecificationId, o.Value, o.Code, o.Description, o.IsActive, o.SortOrder))
                .ToList(),
            s.CreatedAt
        )).ToList();
    }

    public async Task<SpecificationDto> GetSpecificationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _context.Specifications
            .AsNoTracking()
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (s == null) throw new NotFoundException("Specification", id);

        return new SpecificationDto(
            s.Id,
            s.Name,
            s.Code,
            s.Description,
            s.IsActive,
            s.SortOrder,
            s.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Value).Select(o => new SpecificationOptionDto(o.Id, o.SpecificationId, o.Value, o.Code, o.Description, o.IsActive, o.SortOrder)).ToList(),
            s.CreatedAt
        );
    }

    public async Task<SpecificationDto> CreateSpecificationAsync(CreateSpecificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Specification name cannot be empty.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Specification code cannot be empty.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.Specifications.AnyAsync(s => s.Code.ToUpper() == normalizedCode, cancellationToken);
        if (exists) throw new BusinessRuleException($"A specification with code '{request.Code}' already exists.");

        var spec = new Specification
        {
            Name = request.Name.Trim(),
            Code = normalizedCode,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.Specifications.Add(spec);
        RecordAudit("Specification", spec.Id, "CREATE", null, new { spec.Name, spec.Code, spec.Description, spec.IsActive, spec.SortOrder });
        await _context.SaveChangesAsync(cancellationToken);

        return new SpecificationDto(spec.Id, spec.Name, spec.Code, spec.Description, spec.IsActive, spec.SortOrder, new List<SpecificationOptionDto>(), spec.CreatedAt);
    }

    public async Task<SpecificationDto> UpdateSpecificationAsync(Guid id, UpdateSpecificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Specification name cannot be empty.");

        var s = await _context.Specifications.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (s == null) throw new NotFoundException("Specification", id);

        var oldValues = new { s.Name, s.Description, s.IsActive, s.SortOrder };

        s.Name = request.Name.Trim();
        s.Description = request.Description?.Trim() ?? string.Empty;
        s.IsActive = request.IsActive;
        s.SortOrder = request.SortOrder;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        s.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Specification", s.Id, "UPDATE", oldValues, new { s.Name, s.Description, s.IsActive, s.SortOrder });
        await _context.SaveChangesAsync(cancellationToken);

        return new SpecificationDto(
            s.Id,
            s.Name,
            s.Code,
            s.Description,
            s.IsActive,
            s.SortOrder,
            s.Options.OrderBy(o => o.SortOrder).Select(o => new SpecificationOptionDto(o.Id, o.SpecificationId, o.Value, o.Code, o.Description, o.IsActive, o.SortOrder)).ToList(),
            s.CreatedAt
        );
    }

    public async Task<SpecificationDto> ToggleSpecificationStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var s = await _context.Specifications.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (s == null) throw new NotFoundException("Specification", id);

        var previousStatus = s.IsActive;
        s.IsActive = !s.IsActive;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        s.UpdatedBy = _currentUserService.UserId;

        RecordAudit("Specification", s.Id, s.IsActive ? "ACTIVATED" : "DEACTIVATED", new { IsActive = previousStatus }, new { s.IsActive });
        await _context.SaveChangesAsync(cancellationToken);

        return new SpecificationDto(
            s.Id,
            s.Name,
            s.Code,
            s.Description,
            s.IsActive,
            s.SortOrder,
            s.Options.OrderBy(o => o.SortOrder).Select(o => new SpecificationOptionDto(o.Id, o.SpecificationId, o.Value, o.Code, o.Description, o.IsActive, o.SortOrder)).ToList(),
            s.CreatedAt
        );
    }

    public async Task<SpecificationOptionDto> AddSpecificationOptionAsync(Guid specificationId, CreateSpecificationOptionRequest request, CancellationToken cancellationToken = default)
    {
        var spec = await _context.Specifications.FirstOrDefaultAsync(s => s.Id == specificationId, cancellationToken);
        if (spec == null) throw new NotFoundException("Specification", specificationId);

        if (string.IsNullOrWhiteSpace(request.Value)) throw new BusinessRuleException("Option value cannot be empty.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Option code cannot be empty.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.SpecificationOptions
            .AnyAsync(o => o.SpecificationId == specificationId && o.Code.ToUpper() == normalizedCode, cancellationToken);

        if (exists) throw new BusinessRuleException($"Option with code '{request.Code}' already exists for this specification.");

        var option = new SpecificationOption
        {
            SpecificationId = specificationId,
            Value = request.Value.Trim(),
            Code = normalizedCode,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.SpecificationOptions.Add(option);
        RecordAudit("SpecificationOption", option.Id, "CREATE", null, new { option.SpecificationId, option.Value, option.Code, option.Description, option.IsActive, option.SortOrder });
        await _context.SaveChangesAsync(cancellationToken);

        return new SpecificationOptionDto(option.Id, option.SpecificationId, option.Value, option.Code, option.Description, option.IsActive, option.SortOrder);
    }

    public async Task<SpecificationOptionDto> ToggleSpecificationOptionStatusAsync(Guid specificationId, Guid optionId, CancellationToken cancellationToken = default)
    {
        var opt = await _context.SpecificationOptions
            .FirstOrDefaultAsync(o => o.SpecificationId == specificationId && o.Id == optionId, cancellationToken);

        if (opt == null) throw new NotFoundException("SpecificationOption", optionId);

        var prev = opt.IsActive;
        opt.IsActive = !opt.IsActive;
        opt.UpdatedAt = DateTimeOffset.UtcNow;
        opt.UpdatedBy = _currentUserService.UserId;

        RecordAudit("SpecificationOption", opt.Id, opt.IsActive ? "ACTIVATED" : "DEACTIVATED", new { IsActive = prev }, new { opt.IsActive });
        await _context.SaveChangesAsync(cancellationToken);

        return new SpecificationOptionDto(opt.Id, opt.SpecificationId, opt.Value, opt.Code, opt.Description, opt.IsActive, opt.SortOrder);
    }
    #endregion

    #region Stock Locations (Branch Scoped)
    public async Task<List<StockLocationDto>> GetStockLocationsAsync(Guid? branchId = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _context.StockLocations
            .AsNoTracking()
            .Include(l => l.Branch)
            .AsQueryable();

        // Enforce branch boundaries
        if (_currentUserService.IsGlobalAdmin)
        {
            if (branchId.HasValue && branchId != Guid.Empty)
            {
                query = query.Where(l => l.BranchId == branchId.Value);
            }
        }
        else
        {
            var targetBranchId = branchId ?? _currentUserService.BranchId;
            if (!targetBranchId.HasValue)
            {
                // Fallback to accessible branches
                var accessible = _currentUserService.AccessibleBranchIds;
                query = query.Where(l => accessible.Contains(l.BranchId));
            }
            else
            {
                await _branchAuthService.EnsureBranchAccessAsync(targetBranchId.Value, cancellationToken);
                query = query.Where(l => l.BranchId == targetBranchId.Value);
            }
        }

        if (isActive.HasValue)
        {
            query = query.Where(l => l.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Name)
            .Select(l => new StockLocationDto(
                l.Id,
                l.BranchId,
                l.Branch != null ? l.Branch.Code : string.Empty,
                l.Branch != null ? l.Branch.Name : string.Empty,
                l.Name,
                l.Code,
                l.LocationType,
                l.Description,
                l.IsActive,
                l.SortOrder,
                l.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<StockLocationDto> GetStockLocationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var l = await _context.StockLocations
            .AsNoTracking()
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (l == null) throw new NotFoundException("StockLocation", id);

        // Verify user can access this location's branch
        await _branchAuthService.EnsureBranchAccessAsync(l.BranchId, cancellationToken);

        return new StockLocationDto(
            l.Id,
            l.BranchId,
            l.Branch != null ? l.Branch.Code : string.Empty,
            l.Branch != null ? l.Branch.Name : string.Empty,
            l.Name,
            l.Code,
            l.LocationType,
            l.Description,
            l.IsActive,
            l.SortOrder,
            l.CreatedAt
        );
    }

    public async Task<StockLocationDto> CreateStockLocationAsync(CreateStockLocationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Location name cannot be empty.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessRuleException("Location code cannot be empty.");

        // Branch authorization check: must have access to the target branch
        await _branchAuthService.EnsureBranchAccessAsync(request.BranchId, cancellationToken);

        // Branch existence check
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        if (branch == null || !branch.IsActive)
        {
            throw new BusinessRuleException("Specified branch does not exist or is inactive.");
        }

        // Branch uniqueness rule: within a branch, location code must be unique
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _context.StockLocations
            .AnyAsync(l => l.BranchId == request.BranchId && l.Code.ToUpper() == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException($"A stock location with code '{request.Code}' already exists for branch '{branch.Code}'.");
        }

        var location = new StockLocation
        {
            BranchId = request.BranchId,
            Name = request.Name.Trim(),
            Code = normalizedCode,
            LocationType = string.IsNullOrWhiteSpace(request.LocationType) ? "Display" : request.LocationType.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.StockLocations.Add(location);
        RecordAudit("StockLocation", location.Id, "CREATE", null, new { location.BranchId, location.Name, location.Code, location.LocationType, location.Description, location.IsActive, location.SortOrder }, location.BranchId);
        await _context.SaveChangesAsync(cancellationToken);

        return new StockLocationDto(
            location.Id,
            location.BranchId,
            branch.Code,
            branch.Name,
            location.Name,
            location.Code,
            location.LocationType,
            location.Description,
            location.IsActive,
            location.SortOrder,
            location.CreatedAt
        );
    }

    public async Task<StockLocationDto> UpdateStockLocationAsync(Guid id, UpdateStockLocationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Location name cannot be empty.");

        var l = await _context.StockLocations.Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (l == null) throw new NotFoundException("StockLocation", id);

        // Branch boundary check
        await _branchAuthService.EnsureBranchAccessAsync(l.BranchId, cancellationToken);

        var oldValues = new { l.Name, l.LocationType, l.Description, l.IsActive, l.SortOrder };

        l.Name = request.Name.Trim();
        l.LocationType = string.IsNullOrWhiteSpace(request.LocationType) ? "Display" : request.LocationType.Trim();
        l.Description = request.Description?.Trim() ?? string.Empty;
        l.IsActive = request.IsActive;
        l.SortOrder = request.SortOrder;
        l.UpdatedAt = DateTimeOffset.UtcNow;
        l.UpdatedBy = _currentUserService.UserId;

        RecordAudit("StockLocation", l.Id, "UPDATE", oldValues, new { l.Name, l.LocationType, l.Description, l.IsActive, l.SortOrder }, l.BranchId);
        await _context.SaveChangesAsync(cancellationToken);

        return new StockLocationDto(
            l.Id,
            l.BranchId,
            l.Branch != null ? l.Branch.Code : string.Empty,
            l.Branch != null ? l.Branch.Name : string.Empty,
            l.Name,
            l.Code,
            l.LocationType,
            l.Description,
            l.IsActive,
            l.SortOrder,
            l.CreatedAt
        );
    }

    public async Task<StockLocationDto> ToggleStockLocationStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var l = await _context.StockLocations.Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (l == null) throw new NotFoundException("StockLocation", id);

        await _branchAuthService.EnsureBranchAccessAsync(l.BranchId, cancellationToken);

        var prev = l.IsActive;
        l.IsActive = !l.IsActive;
        l.UpdatedAt = DateTimeOffset.UtcNow;
        l.UpdatedBy = _currentUserService.UserId;

        RecordAudit("StockLocation", l.Id, l.IsActive ? "ACTIVATED" : "DEACTIVATED", new { IsActive = prev }, new { l.IsActive }, l.BranchId);
        await _context.SaveChangesAsync(cancellationToken);

        return new StockLocationDto(
            l.Id,
            l.BranchId,
            l.Branch != null ? l.Branch.Code : string.Empty,
            l.Branch != null ? l.Branch.Name : string.Empty,
            l.Name,
            l.Code,
            l.LocationType,
            l.Description,
            l.IsActive,
            l.SortOrder,
            l.CreatedAt
        );
    }
    #endregion

    #region Making Charge Methods
    public List<MakingChargeMethodDto> GetMakingChargeMethods()
    {
        return new List<MakingChargeMethodDto>
        {
            new((int)MakingChargeMethod.PerGram, "Per Gram", "PER_GRAM", "Charge calculated per gram of metal weight (default for standard jewellery)", true),
            new((int)MakingChargeMethod.PerItem, "Per Item", "PER_ITEM", "Fixed making charge per individual jewellery item or artistic unit", false)
        };
    }
    #endregion

    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private void RecordAudit(string entityName, Guid entityId, string action, object? oldValues, object? newValues, Guid? branchId = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Action = action,
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = branchId ?? _currentUserService.BranchId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, AuditJsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, AuditJsonOptions) : null,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}

using AkilliPrompt.Domain.Entities;
using AkilliPrompt.Persistence.EntityFramework.Contexts;
using AkilliPrompt.WebApi.Helpers;
using AkilliPrompt.WebApi.Models;
using AkilliPrompt.WebApi.V1.Models.Categories;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace AkilliPrompt.WebApi.V1.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
[ApiVersion("1.0")]
public sealed class CategoriesController : ControllerBase
{
    private readonly string _allCategoriesCacheKey = "all-categories";
    private readonly string _categoryKeyCachePrefix = "category-";
    private readonly MemoryCacheEntryOptions _cacheOptions;
    private readonly IMemoryCache _memoryCache;
    private readonly ApplicationDbContext _dbContext;

    public CategoriesController(
        IMemoryCache memoryCache,
        ApplicationDbContext dbContext)
    {
        _memoryCache = memoryCache;
        _dbContext = dbContext;

        var slidingExpiration = TimeSpan.FromMinutes(10);
        var absoluteExpiration = TimeSpan.FromHours(24);

        _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(slidingExpiration)
            .SetAbsoluteExpiration(absoluteExpiration);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(_allCategoriesCacheKey, out List<GetAllCategoriesDto> cachedCategories))
            return Ok(cachedCategories);

        var categories = await _dbContext
            .Categories
            .AsNoTracking()
            .Select(category => new GetAllCategoriesDto(category.Id, category.Name))
            .ToListAsync(cancellationToken);

        _memoryCache.Set(_allCategoriesCacheKey, categories, _cacheOptions);

        return Ok(categories);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var cacheKey = $"{_categoryKeyCachePrefix}{id}";

        if (_memoryCache.TryGetValue(cacheKey, out GetByIdCategoryDto cachedCategory))
            return Ok(cachedCategory);

        var category = await _dbContext
            .Categories
            .AsNoTracking()
            .Select(category => new GetByIdCategoryDto(category.Id, category.Name, category.Description))
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

        if (category is null)
            return NotFound();

        _memoryCache.Set(cacheKey, category, _cacheOptions);

        return Ok(category);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(CreateCategoryDto dto, CancellationToken cancellationToken)
    {
        var category = Category.Create(dto.Name, dto.Description);

        _dbContext.Categories.Add(category);

        await _dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCache();

        return Ok(ResponseDto<long>.Success(category.Id, MessageHelper.GetApiSuccessCreatedMessage("Kategori")));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(long id, UpdateCategoryDto dto, CancellationToken cancellationToken)
    {
        if (dto.Id != id)
            return BadRequest();

        var category = await _dbContext
        .Categories
        .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

        if (category is null)
            return NotFound();

        category.Name = dto.Name;
        category.Description = dto.Description;

        await _dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCache(id);

        return Ok(ResponseDto<long>.Success(MessageHelper.GetApiSuccessUpdatedMessage("Kategori")));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await _dbContext
            .Categories
            .Where(category => category.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        if (result == 0)
            return NotFound();

        InvalidateCache(id);

        return Ok(ResponseDto<long>.Success(MessageHelper.GetApiSuccessDeletedMessage("Kategori")));
    }

    private void InvalidateCache(long? categoryId = null)
    {
        _memoryCache.Remove(_allCategoriesCacheKey);

        if (categoryId.HasValue)
            _memoryCache.Remove($"{_categoryKeyCachePrefix}{categoryId}");
    }
}
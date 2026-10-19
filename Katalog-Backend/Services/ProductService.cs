using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Mappers;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services.Interfaces;

namespace Katalog_Backend.Services;

public class ProductService(IProductRepository repository) : IProductService
{
    public async Task<IEnumerable<ProductResponseDto>> GetAllProductsAsync()
    {
        var products = await repository.GetAllAsync();
        return products.Select(p => p.ToDto());
    }

    public async Task<PagedResult<ProductResponseDto>> GetProductsAsync(ProductQueryDto query)
    {
        var (items, totalCount) = await repository.GetPagedAsync(query);
        return new PagedResult<ProductResponseDto>
        {
            Items = items.Select(p => p.ToDto()),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ProductResponseDto> GetProductByIdAsync(int id)
    {
        var product = await repository.GetByIdAsync(id);
        if (product == null)
        {
            throw new ProductNotFoundException(id);
        }

        return product.ToDto();
    }

    public async Task<ProductResponseDto> CreateProductAsync(CreateProductDto dto)
    {
        var entity = dto.ToEntity();
        var created = await repository.CreateAsync(entity);
        return created.ToDto();
    }

    public async Task<ProductResponseDto> UpdateProductAsync(int id, UpdateProductDto dto)
    {
        var existing = await repository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new ProductNotFoundException(id);
        }

        dto.UpdateEntity(existing);
        var updated = await repository.UpdateAsync(existing);
        return updated.ToDto();
    }

    public async Task DeleteProductAsync(int id)
    {
        var deleted = await repository.DeleteAsync(id);
        if (!deleted)
        {
            throw new ProductNotFoundException(id);
        }
    }
}

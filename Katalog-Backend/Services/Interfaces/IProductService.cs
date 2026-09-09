using Katalog_Backend.DTO;

namespace Katalog_Backend.Services.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductResponseDto>> GetAllProductsAsync();
    Task<ProductResponseDto> GetProductByIdAsync(int id);
    Task<ProductResponseDto> CreateProductAsync(CreateProductDto dto);
    Task<ProductResponseDto> UpdateProductAsync(int id, UpdateProductDto dto);
    Task DeleteProductAsync(int id);
}

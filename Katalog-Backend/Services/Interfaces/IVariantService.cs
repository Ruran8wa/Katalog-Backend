using Katalog_Backend.DTO;
using Microsoft.AspNetCore.Http;

namespace Katalog_Backend.Services.Interfaces;

public interface IVariantService
{
    Task<IEnumerable<VariantResponseDto>> GetAllVariantsAsync(int? productId = null);
    Task<VariantResponseDto> GetVariantByIdAsync(int id);
    Task<VariantResponseDto> CreateVariantAsync(CreateVariantDto createDto);
    Task<VariantResponseDto> UpdateVariantAsync(int id, UpdateVariantDto updateDto);
    Task DeleteVariantAsync(int id);

    // Image Operations
    Task<VariantImageResponseDto> UploadVariantImageAsync(int variantId, IFormFile file, bool isPrimary = false, int displayOrder = 0);
    Task DeleteVariantImageAsync(int variantId, int imageId);
    Task SetPrimaryVariantImageAsync(int variantId, int imageId);
}

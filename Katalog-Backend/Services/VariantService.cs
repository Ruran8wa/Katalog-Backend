using Katalog_Backend.DTO;
using Katalog_Backend.Exceptions;
using Katalog_Backend.Mappers;
using Katalog_Backend.Models;
using Katalog_Backend.Repositories.Interfaces;
using Katalog_Backend.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Katalog_Backend.Services;

public class VariantService : IVariantService
{
    private readonly IVariantRepository _variantRepository;
    private readonly IFileStorageService _fileStorageService;

    public VariantService(
        IVariantRepository variantRepository,
        IFileStorageService fileStorageService)
    {
        _variantRepository = variantRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<IEnumerable<VariantResponseDto>> GetAllVariantsAsync(int? productId = null)
    {
        if (productId.HasValue)
        {
            var productExists = await _variantRepository.ProductExistsAsync(productId.Value);
            if (!productExists)
            {
                throw new KeyNotFoundException($"Product with ID {productId.Value} was not found.");
            }
        }

        var variants = await _variantRepository.GetAllAsync(productId);
        return variants.Select(v => v.ToDto());
    }

    public async Task<VariantResponseDto> GetVariantByIdAsync(int id)
    {
        var variant = await _variantRepository.GetByIdAsync(id);
        if (variant == null)
        {
            throw new VariantNotFoundException(id);
        }

        return variant.ToDto();
    }

    public async Task<VariantResponseDto> CreateVariantAsync(CreateVariantDto createDto)
    {
        var productExists = await _variantRepository.ProductExistsAsync(createDto.ProductId);
        if (!productExists)
        {
            throw new KeyNotFoundException($"Product with ID {createDto.ProductId} was not found.");
        }

        if (await _variantRepository.SkuExistsAsync(createDto.Sku))
        {
            throw new DuplicateSkuException(createDto.Sku);
        }

        var variant = createDto.ToEntity();
        var created = await _variantRepository.CreateAsync(variant);
        return created.ToDto();
    }

    public async Task<VariantResponseDto> UpdateVariantAsync(int id, UpdateVariantDto updateDto)
    {
        var variant = await _variantRepository.GetByIdAsync(id);
        if (variant == null)
        {
            throw new VariantNotFoundException(id);
        }

        if (variant.Sku != updateDto.Sku && await _variantRepository.SkuExistsAsync(updateDto.Sku, id))
        {
            throw new DuplicateSkuException(updateDto.Sku);
        }

        updateDto.UpdateEntity(variant);
        var updated = await _variantRepository.UpdateAsync(variant);
        return updated.ToDto();
    }

    public async Task DeleteVariantAsync(int id)
    {
        var variant = await _variantRepository.GetByIdAsync(id);
        if (variant == null)
        {
            throw new VariantNotFoundException(id);
        }

        if (variant.Images != null)
        {
            foreach (var img in variant.Images)
            {
                if (!string.IsNullOrEmpty(img.PublicId))
                {
                    await _fileStorageService.DeleteImageAsync(img.PublicId);
                }
            }
        }

        await _variantRepository.DeleteAsync(id);
    }

    public async Task<VariantImageResponseDto> UploadVariantImageAsync(int variantId, IFormFile file, bool isPrimary = false, int displayOrder = 0)
    {
        var variant = await _variantRepository.GetByIdAsync(variantId);
        if (variant == null)
        {
            throw new VariantNotFoundException(variantId);
        }

        var uploadResult = await _fileStorageService.UploadImageAsync(file, "variants");

        if (isPrimary || variant.Images.Count == 0)
        {
            isPrimary = true;
            foreach (var existingImg in variant.Images)
            {
                existingImg.IsPrimary = false;
            }
        }

        var variantImage = new VariantImage
        {
            ImageUrl = uploadResult.Url,
            PublicId = uploadResult.PublicId,
            IsPrimary = isPrimary,
            DisplayOrder = displayOrder,
            VariantId = variantId,
            Variant = variant
        };

        var createdImage = await _variantRepository.AddImageAsync(variantImage);
        return createdImage.ToDto();
    }

    public async Task DeleteVariantImageAsync(int variantId, int imageId)
    {
        var image = await _variantRepository.GetImageByIdAsync(imageId);
        if (image == null || image.VariantId != variantId)
        {
            throw new VariantImageNotFoundException(imageId);
        }

        if (!string.IsNullOrEmpty(image.PublicId))
        {
            await _fileStorageService.DeleteImageAsync(image.PublicId);
        }

        await _variantRepository.DeleteImageAsync(image);
    }

    public async Task SetPrimaryVariantImageAsync(int variantId, int imageId)
    {
        var image = await _variantRepository.GetImageByIdAsync(imageId);
        if (image == null || image.VariantId != variantId)
        {
            throw new VariantImageNotFoundException(imageId);
        }

        await _variantRepository.SetPrimaryImageAsync(variantId, imageId);
    }
}

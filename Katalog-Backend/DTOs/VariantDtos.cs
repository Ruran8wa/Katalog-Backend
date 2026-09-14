using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Katalog_Backend.DTO;

public class CreateVariantDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int? PriceOverride { get; set; }

    [Required]
    [MaxLength(20)]
    public string Sku { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    public int ProductId { get; set; }
}

public class UpdateVariantDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int? PriceOverride { get; set; }

    [Required]
    [MaxLength(20)]
    public string Sku { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}

public class VariantImageResponseDto
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public class VariantResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? PriceOverride { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public List<VariantImageResponseDto> Images { get; set; } = new();
}

public class UploadVariantImageDto
{
    [Required]
    public IFormFile File { get; set; } = null!;
    public bool IsPrimary { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
}

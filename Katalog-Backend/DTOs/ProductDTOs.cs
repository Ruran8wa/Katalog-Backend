using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.DTO;

public class CreateProductDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int BasePrice { get; set; }

    public string Material { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }
}

public class UpdateProductDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int BasePrice { get; set; }

    public string Material { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }
}

public record ProductResponseDto(
    int Id,
    string Name,
    string Description,
    int BasePrice,
    string Material,
    int CategoryId,
    string? CategoryName
);

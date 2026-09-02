using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.DTO;

public class CreateCategoryDto
{
    [Required]
    public string Name { get; set; }
    public int? CategoryParentId { get; set; }
}

public class UpdateCategoryDto
{
    [Required]
    public int Id { get; set; }
    [Required]
    public string Name { get; set; }
}

public class CategoryResponseDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? CategoryParentId { get; set; }
    public List<CategoryResponseDto>? Children { get; set; }
}
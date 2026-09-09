using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.DTO;

public class CreateCollectionDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class UpdateCollectionDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class CollectionProductResponseDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int BasePrice { get; set; }
    public string Material { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
}

public class CollectionResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CollectionProductResponseDto> Products { get; set; } = new();
}

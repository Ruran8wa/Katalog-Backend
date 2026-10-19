using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.Models;

public class VariantImage
{
    public int Id { get; set; }

    [MaxLength(500)]
    public required string ImageUrl { get; set; }

    [MaxLength(250)]
    public required string PublicId { get; set; }

    public bool IsPrimary { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;

    public int VariantId { get; set; }
    public required Variant Variant { get; set; }
}

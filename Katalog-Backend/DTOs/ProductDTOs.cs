using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.DTO;

/// <summary>Query parameters for filtering and paginating the products list.</summary>
public class ProductQueryDto
{
    /// <summary>Filter by partial product name (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>Filter by category ID.</summary>
    public int? CategoryId { get; set; }

    /// <summary>Filter by material (case-insensitive).</summary>
    public string? Material { get; set; }

    /// <summary>Minimum base price (inclusive).</summary>
    [Range(0, int.MaxValue)]
    public int? MinPrice { get; set; }

    /// <summary>Maximum base price (inclusive).</summary>
    [Range(0, int.MaxValue)]
    public int? MaxPrice { get; set; }

    /// <summary>
    /// Sort field. Allowed values: name, price, id (default: id).
    /// </summary>
    public string SortBy { get; set; } = "id";

    /// <summary>Sort direction: asc or desc (default: asc).</summary>
    public string SortDirection { get; set; } = "asc";

    /// <summary>Page number, 1-based (default: 1).</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Number of results per page (default: 20, max: 100).</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

/// <summary>Generic paginated result envelope.</summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = Enumerable.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

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

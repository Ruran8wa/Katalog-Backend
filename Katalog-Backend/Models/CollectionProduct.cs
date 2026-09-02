using System.ComponentModel.DataAnnotations;

namespace Katalog_Backend.Models;

public class CollectionProduct
{
    public int CollectionId { get; set; }
    public int ProductId { get; set; }
    public required Product Product { get; set; }
    public required Collection Collection { get; set; }
}
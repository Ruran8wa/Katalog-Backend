using Katalog_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Data;

// Stock is only ever changed with single atomic UPDATE statements, never read-modify-write,
// so concurrent orders cannot overwrite each other's changes or oversell.
public static class StockUpdates
{
    // Returns false, changing nothing, when the variant is missing or has fewer than quantity units.
    public static async Task<bool> TryTakeStockAsync(
        this DbSet<Variant> variants, int variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var updated = await variants
            .Where(v => v.Id == variantId && v.Quantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(v => v.Quantity, v => v.Quantity - quantity),
                cancellationToken);
        return updated == 1;
    }
}

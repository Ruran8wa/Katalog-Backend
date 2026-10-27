using dotenv.net;
using Katalog_Backend.Data;
using Katalog_Backend.Enums;
using Katalog_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend.Tests.Helpers;

// Integration tests run against their own database so the Compose consumer,
// which watches KatalogDb, never picks up orders created by a test.
public static class TestDatabase
{
    private static readonly Lazy<string> LazyConnectionString = new(BuildConnectionString);
    private static readonly Lazy<bool> Migrated = new(() =>
    {
        using var context = CreateContext();
        context.Database.Migrate();
        return true;
    });

    public static string ConnectionString => LazyConnectionString.Value;

    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    public static void EnsureMigrated() => _ = Migrated.Value;

    public static async Task<Variant> SeedVariantAsync(int quantity, int basePrice = 100)
    {
        await using var context = CreateContext();
        var product = new Product
        {
            Name = "Test Product",
            BasePrice = basePrice,
            Category = new Category { CategoryName = $"Test-{Guid.NewGuid():N}" },
            Variants = new List<Variant>()
        };
        var variant = new Variant
        {
            Name = "Test Variant",
            Sku = $"T-{Guid.NewGuid():N}"[..20],
            Quantity = quantity,
            Product = product
        };
        context.Variants.Add(variant);
        await context.SaveChangesAsync();
        return variant;
    }

    public static async Task<ApplicationUser> SeedUserAsync()
    {
        await using var context = CreateContext();
        var email = $"{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant()
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<Order> SeedOrderAsync(
        string userId,
        int variantId,
        int quantity,
        OrderStatus status = OrderStatus.Pending,
        int priceAtPurchase = 100)
    {
        await using var context = CreateContext();
        var order = new Order
        {
            UserId = userId,
            VariantId = variantId,
            Quantity = quantity,
            PriceAtPurchase = priceAtPurchase,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            User = null!,
            Variant = null!
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    public static async Task<Order> GetOrderAsync(int orderId)
    {
        await using var context = CreateContext();
        return await context.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
    }

    public static async Task<int> GetStockAsync(int variantId)
    {
        await using var context = CreateContext();
        return await context.Variants.AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => v.Quantity)
            .SingleAsync();
    }

    private static string BuildConnectionString()
    {
        var ciConnectionString = Environment.GetEnvironmentVariable("CI_DATABASE_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(ciConnectionString))
        {
            return ciConnectionString;
        }

        DotEnv.Load(new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 6, overwriteExistingVars: false));
        var password = Environment.GetEnvironmentVariable("SA_PASSWORD") ?? "YourStrong!Passw0rd";
        return $"Server=localhost,1433;Database=KatalogTests;User Id=sa;Password={password};TrustServerCertificate=True;";
    }
}

using Katalog_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend;

public class ApplicationDBContext(DbContextOptions<ApplicationDBContext> options) : DbContext(options)
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Variant> Variants { get; set; }
}
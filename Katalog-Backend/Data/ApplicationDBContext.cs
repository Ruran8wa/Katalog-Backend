using Katalog_Backend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Katalog_Backend;

public class ApplicationDBContext(DbContextOptions<ApplicationDBContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<CollectionProduct> CollectionProducts { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<Variant> Variants { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        builder.Entity<CollectionProduct>().HasKey( x => new {x.CollectionId, x.ProductId});
        builder.Entity<CollectionProduct>().HasOne(x => x.Collection).WithMany(x => x.CollectionProducts).HasForeignKey(x => x.CollectionId);
        builder.Entity<CollectionProduct>().HasOne(x => x.Product).WithMany(x => x.CollectionProducts).HasForeignKey(x => x.ProductId);
        
        builder.Entity<Category>().HasIndex(x => new {x.CategoryParentId, x.CategoryName}).IsUnique();
        builder.Entity<Category>()
            .HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.CategoryParentId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.Entity<Variant>().HasIndex(v => v.SKU).IsUnique();
        builder.Entity<Variant>()
            .HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}

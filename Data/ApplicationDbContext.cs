using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FreyKicksStore.Models;

namespace FreyKicksStore.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<FreyKicksStore.Models.Product> Products { get; set; }
    public DbSet<FreyKicksStore.Models.ProductVariant> ProductVariants { get; set; }
    public DbSet<FreyKicksStore.Models.CartItem> CartItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Store enums as strings for readability and compatibility with existing string data
        modelBuilder.Entity<FreyKicksStore.Models.Product>().Property(p => p.Category).HasConversion<string>();
        modelBuilder.Entity<FreyKicksStore.Models.Product>().Property(p => p.Gender).HasConversion<string>();
    }
}

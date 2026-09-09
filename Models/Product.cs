using System.Collections.Generic;

namespace FreyKicksStore.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProductCategory Category { get; set; } = ProductCategory.General;
    public Gender Gender { get; set; } = Gender.Unisex;
    public string? ImageUrl { get; set; }
    
    // Comma-separated URLs (relative to webroot) for multiple product images
    public string? ImageUrls { get; set; }

    public List<ProductVariant> Variants { get; set; } = new();
}

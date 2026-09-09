namespace FreyKicksStore.Models;

using System;
using System.ComponentModel.DataAnnotations;

public class CartItem
{
    [Key]
    public int Id { get; set; }

    public string? UserId { get; set; }

    public int ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

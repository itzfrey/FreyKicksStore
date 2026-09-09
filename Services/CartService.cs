using System.Collections.Generic;
using System.Linq;
using FreyKicksStore.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace FreyKicksStore.Services;

public class CartService
{
    private readonly List<CartItem> _inMemoryItems = new();
    private readonly FreyKicksStore.Data.ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CartService(FreyKicksStore.Data.ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    private string? CurrentUserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public IReadOnlyList<CartItem> GetItems()
    {
        var userId = CurrentUserId;
        if (!string.IsNullOrEmpty(userId))
        {
            return _db.CartItems.Where(c => c.UserId == userId).ToList();
        }

        return _inMemoryItems;
    }

    public void AddToCart(CartItem item)
    {
        var userId = CurrentUserId;
        if (!string.IsNullOrEmpty(userId))
        {
            var existing = _db.CartItems.SingleOrDefault(c => c.UserId == userId && c.ProductVariantId == item.ProductVariantId);
            if (existing != null)
            {
                existing.Quantity += item.Quantity;
                _db.Update(existing);
            }
            else
            {
                item.UserId = userId;
                _db.Add(item);
            }
            _db.SaveChanges();
            return;
        }

        var memExisting = _inMemoryItems.FirstOrDefault(i => i.ProductVariantId == item.ProductVariantId);
        if (memExisting != null)
        {
            memExisting.Quantity += item.Quantity;
        }
        else
        {
            _inMemoryItems.Add(item);
        }
    }

    public void RemoveFromCart(int cartItemId)
    {
        var userId = CurrentUserId;
        if (!string.IsNullOrEmpty(userId))
        {
            var item = _db.CartItems.SingleOrDefault(c => c.Id == cartItemId && c.UserId == userId);
            if (item != null)
            {
                _db.CartItems.Remove(item);
                _db.SaveChanges();
            }
            return;
        }

        _inMemoryItems.RemoveAll(i => i.Id == cartItemId);
    }

    public void UpdateQuantity(int cartItemId, int quantity)
    {
        var userId = CurrentUserId;
        if (!string.IsNullOrEmpty(userId))
        {
            var item = _db.CartItems.SingleOrDefault(c => c.Id == cartItemId && c.UserId == userId);
            if (item != null)
            {
                item.Quantity = quantity;
                _db.Update(item);
                _db.SaveChanges();
            }
            return;
        }

        var mem = _inMemoryItems.SingleOrDefault(i => i.Id == cartItemId);
        if (mem != null)
        {
            mem.Quantity = quantity;
        }
    }

    public void Clear() 
    {
        var userId = CurrentUserId;
        if (!string.IsNullOrEmpty(userId))
        {
            var items = _db.CartItems.Where(c => c.UserId == userId).ToList();
            _db.CartItems.RemoveRange(items);
            _db.SaveChanges();
            return;
        }

        _inMemoryItems.Clear();
    }
}

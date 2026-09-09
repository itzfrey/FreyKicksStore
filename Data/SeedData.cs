using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;
using System;

namespace FreyKicksStore.Data;

public static class SeedData
{
        public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<Models.ApplicationUser>>();

        var roles = new[] { "Admin", "Customer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                Console.WriteLine($"Creating role: {role}");
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Support both legacy keys and the newer Seed:AdminEmail key
        var adminEmail = config["AdminUser:Email"] ?? config["Seed:AdminEmail"];
        var adminPassword = config["AdminUser:Password"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                Console.WriteLine($"Creating admin user: {adminEmail}");
                admin = new Models.ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(admin, adminPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(admin, "Admin");
                        Console.WriteLine("Admin user created and added to Admin role.");
                    }
                else
                {
                    Console.WriteLine("Failed to create admin user:");
                    foreach (var err in result.Errors)
                    {
                        Console.WriteLine(err.Description);
                    }
                }
            }
            else
            {
                Console.WriteLine("Admin user already exists.");
                // Ensure the existing user is in the Admin role
                if (!await userManager.IsInRoleAsync(admin, "Admin"))
                {
                    var addRoleResult = await userManager.AddToRoleAsync(admin, "Admin");
                    if (addRoleResult.Succeeded)
                    {
                        Console.WriteLine($"Promoted existing user {adminEmail} to Admin role.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to promote existing user {adminEmail} to Admin role:");
                        foreach (var err in addRoleResult.Errors)
                        {
                            Console.WriteLine(err.Description);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Existing user {adminEmail} is already in Admin role.");
                }
            }
        }
        else
        {
            Console.WriteLine("AdminUser credentials not configured; skipping admin creation.");
        }

        // Seed sample products if none exist
        try
        {
            var context = sp.GetService<ApplicationDbContext>();
            if (context != null)
            {
                if (!context.Products.Any())
                {
                    Console.WriteLine("Seeding sample products...");
                    var p1 = new FreyKicksStore.Models.Product
                    {
                        Name = "Frey Classic",
                        Description = "Everyday comfortable sneaker.",
                        Category = FreyKicksStore.Models.ProductCategory.Sneakers,
                        Gender = FreyKicksStore.Models.Gender.Unisex,
                        ImageUrl = "/images/products/frey-classic.jpg",
                        Variants = new List<FreyKicksStore.Models.ProductVariant>
                        {
                            new() { Sku = "FREY-CL-001", Price = 59.99m, Size = "9", Color = "White", Stock = 10 },
                            new() { Sku = "FREY-CL-002", Price = 59.99m, Size = "10", Color = "White", Stock = 8 }
                        }
                    };

                    var p2 = new FreyKicksStore.Models.Product
                    {
                        Name = "Frey Runner",
                        Description = "Lightweight running shoe.",
                        Category = FreyKicksStore.Models.ProductCategory.Running,
                        Gender = FreyKicksStore.Models.Gender.Men,
                        ImageUrl = "/images/products/frey-runner.jpg",
                        Variants = new List<FreyKicksStore.Models.ProductVariant>
                        {
                            new() { Sku = "FREY-RN-001", Price = 79.99m, Size = "9", Color = "Black", Stock = 5 }
                        }
                    };

                    context.Products.AddRange(p1, p2);
                    context.SaveChanges();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("SeedData product seeding failed: " + ex.Message);
        }
    }
}

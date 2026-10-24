using Microsoft.AspNetCore.Identity;

namespace FreyKicksStore.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "Nigeria";
}

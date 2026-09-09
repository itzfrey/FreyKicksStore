using Microsoft.AspNetCore.Identity;

namespace FreyKicksStore.Models;

public class ApplicationUser : IdentityUser
{
	public string? FullName { get; set; }
}

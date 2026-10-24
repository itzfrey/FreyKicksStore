using System.Security.Claims;
using System.Text.RegularExpressions;
using FreyKicksStore.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FreyKicksStore.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;
    private readonly FreyKicksStore.Services.CartService _cartService;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<AccountController> logger, FreyKicksStore.Services.CartService cartService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _cartService = cartService;
    }

    [HttpGet("external-login")]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, string returnUrl = "/")
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("external-login-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "/")
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            return Redirect("/account/login-ui?error=no_external_info");
        }

        // Log all incoming external claims for debugging
            try
            {
                _logger.LogDebug("External login from provider {Provider}", info.LoginProvider);
                foreach (var claim in info.Principal.Claims)
                {
                    _logger.LogDebug("Claim: {Type} = {Value}", claim.Type, claim.Value);
                }
            }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log external claims");
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        if (signInResult.Succeeded)
        {
            // Ensure FullName is set for existing users who already have the external login linked
            try
            {
                var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (existingUser != null)
                {
                    var givenName = info.Principal.FindFirstValue("given_name");
                    var familyName = info.Principal.FindFirstValue("family_name");
                    var fullNameClaim = info.Principal.FindFirstValue(ClaimTypes.Name) ?? info.Principal.FindFirstValue("name");
                    var fullName = !string.IsNullOrEmpty(givenName) || !string.IsNullOrEmpty(familyName)
                        ? string.Join(' ', new[] { givenName, familyName }.Where(s => !string.IsNullOrEmpty(s)))
                        : fullNameClaim;

                    if (string.IsNullOrEmpty(existingUser.FullName) && !string.IsNullOrEmpty(fullName))
                    {
                        existingUser.FullName = fullName;
                        await _userManager.UpdateAsync(existingUser);
                        _logger.LogInformation("Backfilled FullName for existing user {Email}", existingUser.Email);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backfill FullName for existing user");
            }

            try
            {
                MergeGuestCartFromCookie();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to merge guest cart after external sign-in");
            }

            return LocalRedirect(GetSafeLocalReturnUrl(returnUrl));
        }

        // If user does not exist, create one using email claim and persist Google profile data
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(email))
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Try to get the full name from claims. Google may provide 'given_name' and 'family_name',
            // or a combined ClaimTypes.Name / 'name'. Prefer given+family when available.
            var givenName = info.Principal.FindFirstValue("given_name");
            var familyName = info.Principal.FindFirstValue("family_name");
            var fullNameClaim = info.Principal.FindFirstValue(ClaimTypes.Name) ?? info.Principal.FindFirstValue("name");
            var fullName = !string.IsNullOrEmpty(givenName) || !string.IsNullOrEmpty(familyName)
                ? string.Join(' ', new[] { givenName, familyName }.Where(s => !string.IsNullOrEmpty(s)))
                : fullNameClaim;

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return Redirect("/account/login-ui?error=create_user_failed");
                }

                // Default new users into the Customer role
                await _userManager.AddToRoleAsync(user, "Customer");
            }
            else
            {
                // Update FullName if missing or empty
                if (string.IsNullOrEmpty(user.FullName) && !string.IsNullOrEmpty(fullName))
                {
                    user.FullName = fullName;
                    await _userManager.UpdateAsync(user);
                }
            }

            var addLogin = await _userManager.AddLoginAsync(user, info);
            if (addLogin.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);

                try
                {
                    MergeGuestCartFromCookie();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to merge guest cart after external sign-in (new account)");
                }

                return LocalRedirect(GetSafeLocalReturnUrl(returnUrl));
            }
            else
            {
                return Redirect("/account/login-ui?error=add_login_failed");
            }
        }

        return Redirect("/account/login-ui?error=external_login_failed");
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login()
    {
        return Redirect("/account/login-ui");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginPost([FromForm] string email, [FromForm] string password, [FromForm] bool rememberMe, [FromForm] string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Redirect("/account/login-ui?error=invalid_credentials");
        }

        var normalizedEmail = email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            return Redirect("/account/login-ui?error=invalid_credentials");
        }

        var userName = user.UserName ?? user.Email ?? string.Empty;
        var result = await _signInManager.PasswordSignInAsync(userName, password, rememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            try
            {
                MergeGuestCartFromCookie();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to merge guest cart after email sign-in");
            }

            var safeReturnUrl = GetSafeLocalReturnUrl(returnUrl);
            return LocalRedirect(safeReturnUrl);
        }

        if (result.IsLockedOut)
        {
            return Redirect("/account/login-ui?error=locked_out");
        }

        return Redirect("/account/login-ui?error=invalid_credentials");
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPost([FromForm] string fullName, [FromForm] string email, [FromForm] string password, [FromForm] string confirmPassword, [FromForm] string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            return Redirect("/account/register-ui?error=missing_fields");
        }

        var normalizedEmail = email.Trim();
        if (!Regex.IsMatch(normalizedEmail, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$"))
        {
            return Redirect("/account/register-ui?error=invalid_email");
        }

        if (password != confirmPassword)
        {
            return Redirect("/account/register-ui?error=passwords_do_not_match");
        }

        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            return Redirect("/account/register-ui?error=duplicate_email");
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = fullName.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errorCode = GetRegistrationErrorCode(createResult.Errors);
            return Redirect($"/account/register-ui?error={errorCode}");
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Customer");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return Redirect("/account/register-ui?error=role_assignment_failed");
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        try
        {
            MergeGuestCartFromCookie();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to merge guest cart after email registration");
        }

        var safeReturnUrl = GetSafeLocalReturnUrl(returnUrl);
        return LocalRedirect(safeReturnUrl);
    }

    private static string GetRegistrationErrorCode(IEnumerable<IdentityError> errors)
    {
        if (errors.Any(e => e.Code == "DuplicateUserName" || e.Code == "DuplicateEmail"))
        {
            return "duplicate_email";
        }

        if (errors.Any(e => e.Code == "PasswordTooShort"))
        {
            return "password_too_short";
        }

        if (errors.Any(e => e.Code == "InvalidEmail" || e.Code == "InvalidUserName"))
        {
            return "invalid_email";
        }

        return "create_user_failed";
    }

    private string GetSafeLocalReturnUrl(string? returnUrl)
    {
        return Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
    }

    private void MergeGuestCartFromCookie()
    {
        if (!Request.Cookies.TryGetValue("guest_cart", out var cookie))
            return;

        try
        {
            var json = System.Net.WebUtility.UrlDecode(cookie);
            var items = System.Text.Json.JsonSerializer.Deserialize<List<CartItem>>(json);
            if (items != null && items.Count > 0)
            {
                foreach (var it in items)
                {
                    var existing = _cartService.GetItems().SingleOrDefault(c => c.ProductVariantId == it.ProductVariantId);
                    if (existing != null)
                    {
                        var newQty = existing.Quantity + it.Quantity;
                        _cartService.UpdateQuantity(existing.Id, newQty);
                    }
                    else
                    {
                        _cartService.AddToCart(new CartItem
                        {
                            ProductVariantId = it.ProductVariantId,
                            Sku = it.Sku,
                            ProductName = it.ProductName,
                            Price = it.Price,
                            Quantity = it.Quantity
                        });
                    }
                }
            }

            Response.Cookies.Delete("guest_cart");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse or merge guest_cart cookie");
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    [HttpGet("login-success")]
    [AllowAnonymous]
    public IActionResult LoginSuccess()
    {
        var html = @"<!DOCTYPE html><html><body style='font-family:sans-serif;text-align:center;padding-top:40px;'>
            <p>Signed in successfully. You can close this window.</p>
            <script>
                if (window.opener) {
                    window.opener.postMessage('google-login-success', window.location.origin);
                    window.close();
                } else {
                    window.location.href = '/';
                }
            </script>
        </body></html>";
        return Content(html, "text/html");
}

}

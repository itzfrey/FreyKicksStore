using FreyKicksStore.Components;
using FreyKicksStore.Data;
using FreyKicksStore.Models;
using Microsoft.AspNetCore.Identity;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Add controllers so we can handle external login callbacks
builder.Services.AddControllersWithViews();

// Provide a cascading AuthenticationState for Blazor components
builder.Services.AddCascadingAuthenticationState();

// Cart service (in-memory, scoped to connection) — replace with session/local storage later
builder.Services.AddScoped<FreyKicksStore.Services.CartService>();
builder.Services.AddHttpContextAccessor();

// Configure Google authentication (ClientId/ClientSecret via user-secrets or config)
builder.Services.AddAuthentication()
    .AddGoogle("Google", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        options.CallbackPath = "/signin-google";
        // Do not persist OAuth tokens for Google sign-in-only usage
        options.SaveTokens = false;
        // Ensure profile and email scopes are requested
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.SignInScheme = IdentityConstants.ExternalScheme;
        // No development backchannel override — use default HTTP handler
    });

// Ensure unauthenticated requests redirect to the app login UI and
// requests from authenticated users without the required role go to an access-denied page.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login-ui";
    options.AccessDeniedPath = "/account/access-denied";

    // For interactive/Blazor requests, prefer returning the status code instead of an automatic
    // HTML redirect so the client-side navigation can handle authentication flows properly.
    options.Events = new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            // If the request is an XHR/fetch or Blazor endpoint, return 401 so the client can redirect.
            if (ctx.Request.Headers["X-Requested-With"] == "XMLHttpRequest" || ctx.Request.Path.StartsWithSegments("/_blazor"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Headers["X-Requested-With"] == "XMLHttpRequest" || ctx.Request.Path.StartsWithSegments("/_blazor"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// Support a seed-only run mode: `dotnet run -- --seed-only`
if (args is not null && args.Contains("--seed-only"))
{
    await SeedData.InitializeAsync(app.Services, app.Configuration);
    Console.WriteLine("Seed-only run complete.");
    return;
}

// Initialize seed data (roles + admin user) if configured via configuration/user-secrets.
await SeedData.InitializeAsync(app.Services, app.Configuration);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Only show the Not Found page for true 404s. 401/403 are handled by
// cookie auth configuration (they'll redirect to login or access-denied).
app.UseStatusCodePages(context =>
{
    var status = context.HttpContext.Response.StatusCode;
    if (status == Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound)
    {
        context.HttpContext.Response.Redirect("/not-found");
    }

    return System.Threading.Tasks.Task.CompletedTask;
});
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();

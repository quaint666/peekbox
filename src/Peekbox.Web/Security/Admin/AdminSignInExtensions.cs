using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Peekbox.Web.Data;
using Peekbox.Web.Navigation;

namespace Peekbox.Web.Security.Admin;

// The admin's sign-in (F56, F69, issue #105, Option 2), in one place so Program.cs and the tests use the same settings.
// The framework's Identity library does the hard parts (password hashing, roles, the security stamp); our own pages are
// everything a person sees; AdminLockout counts wrong passwords atomically; AdminCookieEvents reads the role and the on/off
// flag from the database on every request. The cookie is a session cookie (gone when the browser closes), HttpOnly, Secure
// whenever the request is https (ForwardedHeaders makes that true behind the proxy), SameSite Strict, and an hour long counted
// from the last request.
public static class AdminSignInExtensions
{
    public const int PasswordMinLength = 10;

    // A password longer than this is refused before it is hashed: hashing a megabyte would cost real time for nothing.
    public const int PasswordMaxLength = 128;

    public static IServiceCollection AddAdminSignIn(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(AdminOwnerSettings.FromConfiguration(configuration));
        services.AddHostedService<AdminBootstrap>();
        services.AddScoped<AdminCookieEvents>();
        services.AddScoped<AdminLockout>();
        services
            .AddIdentityCore<AdminUser>(options =>
            {
                // Length, not a mix of kinds of characters, is what makes a password hard to guess.
                options.Password.RequiredLength = PasswordMinLength;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                // 5 wrong passwords lock the account for 15 minutes (the placeholders of the login mockup). The counting is
                // AdminLockout's, which reads these two numbers.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<PeekboxDbContext>()
            .AddSignInManager()
            .AddErrorDescriber<VietnameseIdentityErrors>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "Peekbox.Admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromHours(1);
                options.SlidingExpiration = true;
                options.LoginPath = SiteLinks.AdminLoginPath;
                options.AccessDeniedPath = SiteLinks.AdminAccessDeniedPath;
                options.ReturnUrlParameter = "returnUrl";
                options.EventsType = typeof(AdminCookieEvents);
            });
        services.AddAuthorization(AdminPolicies.Register);
        return services;
    }
}

using Microsoft.Extensions.Options;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;

namespace Peekbox.Web.Security.Admin;

// The admin's sign-in cookie says who the person is; it is never trusted for what they may do. On every request this reads
// the person's row and: signs them out if the row is gone or switched off (IsActive), or the password changed or "sign out
// of every device" was pressed elsewhere (the security stamp differs), and otherwise replaces the cookie's role claims
// with the roles the database holds right now. So a changed role or a switched-off person counts at once, not when the cookie
// ends. The framework's own validator only checks the stamp every 30 minutes by default, which is too slow for that.
// One small query per admin request; the shop's pages never run it (they carry no admin cookie).
public sealed class AdminCookieEvents(PeekboxDbContext db, IOptions<IdentityOptions> identityOptions, TimeProvider clock) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var options = identityOptions.Value.ClaimsIdentity;
        if (principal is null || !int.TryParse(principal.FindFirstValue(options.UserIdClaimType), out var userId))
        {
            await Reject(context);
            return;
        }

        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.IsActive,
                u.SecurityStamp,
                u.LockoutEnd,
                u.DisplayName,
                Roles = db.UserRoles.Where(ur => ur.UserId == u.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!).ToList(),
            })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        // A locked account (too many wrong passwords, here or in "change password") is signed out too: the lockout is also the
        // way to stop someone who holds a stolen cookie from guessing the current password.
        if (row is null || !row.IsActive || row.LockoutEnd > clock.GetUtcNow() || row.SecurityStamp != principal.FindFirstValue(options.SecurityStampClaimType))
        {
            await Reject(context);
            return;
        }

        var identity = new ClaimsIdentity(principal.Identity);
        foreach (var claim in identity.FindAll(options.RoleClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaims(row.Roles.Select(role => new Claim(options.RoleClaimType, role)));

        // The name on the account chip is read fresh too, so a changed name shows at once.
        foreach (var claim in identity.FindAll(AdminClaims.DisplayName).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(AdminClaims.DisplayName, row.DisplayName));
        context.ReplacePrincipal(new ClaimsPrincipal(identity));
    }

    private static async Task Reject(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}

public static class AdminClaims
{
    // Added by AdminCookieEvents on every request; never stored in the cookie.
    public const string DisplayName = "peekbox:display-name";
}

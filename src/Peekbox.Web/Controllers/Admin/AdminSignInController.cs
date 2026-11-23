using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Peekbox.Web.Data;
using Peekbox.Web.Formatting;
using Peekbox.Web.Models.Admin;
using Peekbox.Web.Navigation;
using Peekbox.Web.Security.Admin;
using Peekbox.Web.Services;

namespace Peekbox.Web.Controllers.Admin;

// Sign in and out of the admin (F56). The only admin controller that lets anyone in; AdminGuardTests lists these actions and
// fails for any other admin action that does not ask for a role.
//
// Every wrong try looks the same to the person ("Tên đăng nhập hoặc mật khẩu không đúng"): an unknown name, a wrong password and a
// switched-off account. An unknown or switched-off name still costs one password hash, so the time does not tell them apart.
// A good password signs in; a wrong one counts against the account (5 in a row lock it for 15 minutes) and against the address
// (RateLimits). There is no "remember me": the cookie is a session cookie of an hour, counted from the last request.
[AdminArea]
[AllowAnonymous]
public class AdminSignInController(
    SignInManager<AdminUser> signIn, UserManager<AdminUser> users, IPasswordHasher<AdminUser> hasher, AdminLockout lockout, RateLimits rateLimits,
    IOptions<IdentityOptions> identityOptions, TimeProvider clock) : Controller
{
    private const string NoticeKey = "adminNotice";

    // A hash to check a password against when there is no such user, so that answering takes as long as for a real one.
    private static readonly string DummyHash = new PasswordHasher<AdminUser>().HashPassword(new AdminUser { DisplayName = "-" }, Guid.NewGuid().ToString("N"));

    [HttpGet(SiteLinks.AdminLoginPath)]
    [HttpHead(SiteLinks.AdminLoginPath)]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturn(returnUrl));
        }

        return View(new AdminLoginViewModel("", null, SafeReturn(returnUrl), false, TempData[NoticeKey] as string));
    }

    [HttpPost(SiteLinks.AdminLoginPath)]
    public async Task<IActionResult> Login(string? userName, string? password, string? returnUrl, CancellationToken cancellationToken)
    {
        userName = (userName ?? "").Trim();
        var back = SafeReturn(returnUrl);
        if (userName.Length == 0 || string.IsNullOrEmpty(password))
        {
            return View(new AdminLoginViewModel(userName, "Nhập tên đăng nhập và mật khẩu.", back, false));
        }

        // Longer than any real name or password: a wrong try without any hashing (a megabyte of text would cost real time).
        if (userName.Length > AdminUser.UserNameMaxLength || password.Length > AdminSignInExtensions.PasswordMaxLength)
        {
            return Refused(userName.Length > AdminUser.UserNameMaxLength ? "" : userName, back);
        }

        if (rateLimits.AdminLoginBlocked(HttpContext))
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return View(new AdminLoginViewModel(userName, "Bạn đã thử quá nhiều lần. Hãy đợi vài phút rồi thử lại.", back, false));
        }

        var user = await users.FindByNameAsync(userName);
        if (user is null || !user.IsActive)
        {
            hasher.VerifyHashedPassword(user ?? new AdminUser { DisplayName = "-" }, DummyHash, password);
            return Refused(userName, back);
        }

        // Locked already: even the right password is refused, and it does not count as a new wrong one.
        if (AdminLockout.LockedUntil(user, clock.GetUtcNow()) is { } lockedUntil)
        {
            rateLimits.RecordAdminLoginFailure(HttpContext);
            return LockedPage(lockedUntil, userName, back);
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            // Counted by one UPDATE (AdminLockout), so guesses sent at the same moment are all counted. The try that
            // makes the 5th says so at once, not on the next.
            rateLimits.RecordAdminLoginFailure(HttpContext);
            return await lockout.RecordFailureAsync(user.Id, cancellationToken) is { } nowLockedUntil
                ? LockedPage(nowLockedUntil, userName, back)
                : Refused(userName, back, record: false);
        }

        await lockout.ResetAsync(user.Id, cancellationToken);
        await signIn.SignInAsync(user, isPersistent: false);
        return LocalRedirect(back);
    }

    [HttpPost(SiteLinks.AdminLogoutPath)]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        TempData[NoticeKey] = "Bạn đã đăng xuất.";
        return LocalRedirect(SiteLinks.AdminLoginPath);
    }

    private ViewResult Refused(string userName, string back, bool record = true)
    {
        if (record)
        {
            rateLimits.RecordAdminLoginFailure(HttpContext);
        }

        return View("Login", new AdminLoginViewModel(userName, "Tên đăng nhập hoặc mật khẩu không đúng.", back, false));
    }

    private ViewResult LockedPage(DateTimeOffset until, string userName, string back)
    {
        var options = identityOptions.Value.Lockout;
        return View("Login", new AdminLoginViewModel(
            userName,
            $"Đã nhập sai {options.MaxFailedAccessAttempts} lần. Vì an toàn, đăng nhập bị khóa {(int)options.DefaultLockoutTimeSpan.TotalMinutes} phút. Thử lại sau {VnDate.Clock(until)}.",
            back,
            Locked: true));
    }

    // Only an address inside the admin: a link made to send someone elsewhere after signing in is ignored.
    private string SafeReturn(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith(SiteLinks.AdminPath + "/", StringComparison.Ordinal)
            ? returnUrl
            : SiteLinks.AdminOrdersPath;
}

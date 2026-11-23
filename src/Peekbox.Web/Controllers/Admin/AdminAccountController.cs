using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Data;
using Peekbox.Web.Models.Admin;
using Peekbox.Web.Navigation;
using Peekbox.Web.Security.Admin;

namespace Peekbox.Web.Controllers.Admin;

// The signed-in person's own account (F68 and the account screen of F59): change the password, and sign out of every device.
// /admin leads to the orders. The 2-step code card of the mockup comes with its own change.
public class AdminAccountController(SignInManager<AdminUser> signIn, UserManager<AdminUser> users, AdminLockout lockout, TimeProvider clock) : AdminControllerBase
{
    private const string NoticeKey = "adminNotice";

    [HttpGet(SiteLinks.AdminPath)]
    public IActionResult Home() => LocalRedirect(SiteLinks.AdminOrdersPath);

    [HttpGet(SiteLinks.AdminAccountPath)]
    [HttpHead(SiteLinks.AdminAccountPath)]
    public IActionResult Account() => View("Account", Page(null));

    // Signed in, but the role is not enough for the screen asked for.
    [HttpGet(SiteLinks.AdminAccessDeniedPath)]
    public IActionResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("Denied");
    }

    [HttpPost(SiteLinks.AdminPasswordPath)]
    public async Task<IActionResult> ChangePassword(string? currentPassword, string? newPassword, string? confirmPassword)
    {
        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
        {
            return View("Account", Page("Nhập mật khẩu hiện tại và mật khẩu mới hai lần."));
        }

        if (currentPassword.Length > AdminSignInExtensions.PasswordMaxLength || newPassword.Length > AdminSignInExtensions.PasswordMaxLength)
        {
            return View("Account", Page($"Mật khẩu dài tối đa {AdminSignInExtensions.PasswordMaxLength} ký tự."));
        }

        if (newPassword != confirmPassword)
        {
            return View("Account", Page("Hai mật khẩu mới không giống nhau."));
        }

        if (newPassword == currentPassword)
        {
            return View("Account", Page("Mật khẩu mới phải khác mật khẩu hiện tại."));
        }

        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return LocalRedirect(SiteLinks.AdminLoginPath);
        }

        // The current password is checked here and counted (5 wrong in a row lock the account, as at sign-in, one UPDATE each),
        // so the form cannot be used to guess the password from a stolen cookie.
        if (AdminLockout.LockedUntil(user, clock.GetUtcNow()) is not null)
        {
            return LocalRedirect(SiteLinks.AdminLoginPath);   // the cookie check signs this person out as well
        }

        if (!await users.CheckPasswordAsync(user, currentPassword))
        {
            return await lockout.RecordFailureAsync(user.Id) is not null
                ? LocalRedirect(SiteLinks.AdminLoginPath)
                : View("Account", Page("Mật khẩu hiện tại không đúng."));
        }

        await lockout.ResetAsync(user.Id);

        var changed = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changed.Succeeded)
        {
            return View("Account", Page(string.Join(" ", changed.Errors.Select(e => e.Description))));
        }

        // A new password changes the security stamp, which ends every other session; this one stays.
        await signIn.RefreshSignInAsync(user);
        TempData[NoticeKey] = "Đã đổi mật khẩu. Các thiết bị khác đã bị đăng xuất.";
        return LocalRedirect(SiteLinks.AdminAccountPath);
    }

    [HttpPost(SiteLinks.AdminSignOutEverywherePath)]
    public async Task<IActionResult> SignOutEverywhere()
    {
        var user = await users.GetUserAsync(User);
        if (user is not null)
        {
            await users.UpdateSecurityStampAsync(user);
        }

        await signIn.SignOutAsync();
        TempData[NoticeKey] = "Đã đăng xuất khỏi mọi thiết bị. Hãy đăng nhập lại.";
        return LocalRedirect(SiteLinks.AdminLoginPath);
    }

    private AdminAccountViewModel Page(string? passwordError) =>
        new(DisplayName, RoleLabel, User.Identity?.Name ?? "", passwordError, TempData[NoticeKey] as string);
}

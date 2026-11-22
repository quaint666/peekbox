using Peekbox.Web.Security.Admin;

namespace Peekbox.Web.Navigation;

// One screen in the admin's menu (the sidebar on a wide screen, the row of pills on a phone). Roles lists who sees it; the
// server checks the role again on the screen itself, so a hidden link is only tidiness.
public sealed record AdminNavItem(string Key, string Label, string Href, IReadOnlyList<string> Roles);

// The admin's menu, in the order of the mockups. Only the screens that exist are listed: each new screen adds its line here.
public static class AdminNav
{
    private static readonly IReadOnlyList<string> Everyone = AdminRoles.All;

    public static readonly AdminNavItem Account = new("account", "Tài khoản", SiteLinks.AdminAccountPath, Everyone);

    public static readonly IReadOnlyList<AdminNavItem> All = [Account];

    public static IReadOnlyList<AdminNavItem> For(System.Security.Claims.ClaimsPrincipal user) =>
        All.Where(item => item.Roles.Any(user.IsInRole)).ToList();
}

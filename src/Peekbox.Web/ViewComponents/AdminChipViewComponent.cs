using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Models.Admin;
using Peekbox.Web.Security.Admin;

namespace Peekbox.Web.ViewComponents;

// The account chip (F68): initials, name and role of the person signed in. "Sidebar" is the chip at the bottom of the wide
// layout's sidebar, with its "Đăng xuất" button; "Phone" is the round initials button in the phone's top bar and the small
// menu it opens. The name and the role come from the claims AdminCookieEvents has just read from the database.
public class AdminChipViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string variant)
    {
        var name = UserClaimsPrincipal.FindFirstValue(AdminClaims.DisplayName) ?? "";
        var role = AdminRoles.All.FirstOrDefault(UserClaimsPrincipal.IsInRole) ?? "";
        return View(variant, new AdminChipViewModel(AdminInitials.From(name), name, AdminRoles.Label(role)));
    }
}

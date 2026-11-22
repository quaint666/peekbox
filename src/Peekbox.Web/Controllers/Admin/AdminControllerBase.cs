using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Security.Admin;

namespace Peekbox.Web.Controllers.Admin;

// The base of every admin screen (issue #105): signed-in staff only, never cached, never indexed. A screen that needs more than
// "any staff member" says so on the action or the class (AdminPolicies.ManagerUp, OwnerOnly); AdminGuardTests fails for an
// admin action that has neither this base nor an explicit [AllowAnonymous].
[AdminArea]
[Authorize(Policy = AdminPolicies.AnyStaff)]
public abstract class AdminControllerBase : Controller
{
    protected string DisplayName => User.FindFirstValue(AdminClaims.DisplayName) ?? "";

    protected string RoleLabel => AdminRoles.Label(AdminRoles.All.FirstOrDefault(User.IsInRole) ?? "");

}

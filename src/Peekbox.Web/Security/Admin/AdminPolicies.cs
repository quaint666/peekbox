using Microsoft.AspNetCore.Authorization;

namespace Peekbox.Web.Security.Admin;

// Who may open what in the admin. Each policy is a role list; the role comes from the database on every request
// (AdminCookieEvents), so a changed role or a switched-off person counts at once.
public static class AdminPolicies
{
    // Any signed-in staff member: the account page and the admin shell.
    public const string AnyStaff = "AdminAnyStaff";

    // Owner or Manager: day-to-day screens (orders with refunds, items, coupons, ...).
    public const string ManagerUp = "AdminManagerUp";

    // Owner only: settings, users, 2-step code.
    public const string OwnerOnly = "AdminOwnerOnly";

    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(AnyStaff, policy => policy.RequireRole(AdminRoles.Owner, AdminRoles.Manager, AdminRoles.Staff));
        options.AddPolicy(ManagerUp, policy => policy.RequireRole(AdminRoles.Owner, AdminRoles.Manager));
        options.AddPolicy(OwnerOnly, policy => policy.RequireRole(AdminRoles.Owner));
    }
}

using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Controllers.Admin;
using Peekbox.Web.Security.Admin;

using System.Security.Claims;

namespace Peekbox.Tests;

// The rule of the admin (issue #105): the server checks who may do what, on every action; a hidden link is only tidiness. These
// tests fail the moment an admin action is added that does not ask for a role, and check what each role may open.
[Collection(DatabaseCollection.Name)]
public class AdminGuardTests(PeekboxFixture fixture)
{
    // The only admin actions anyone may call without signing in: the sign-in page and the form it sends, and sign-out.
    private static readonly string[] Anonymous =
    [
        "AdminSignInController.Login/GET", "AdminSignInController.Login/POST", "AdminSignInController.Logout/POST",
    ];

    private static IEnumerable<MethodInfo> AdminActions() =>
        typeof(AdminControllerBase).Assembly.GetTypes()
            .Where(t => t.Namespace == "Peekbox.Web.Controllers.Admin" && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static string Name(MethodInfo m) => $"{m.DeclaringType!.Name}.{m.Name}";

    [Fact]
    public void Every_admin_action_asks_for_a_role_or_is_one_of_the_three_that_must_not()
    {
        var actions = AdminActions().ToList();
        Assert.NotEmpty(actions);

        // "Derives from the base" only counts if the base itself asks for a signed-in staff member.
        Assert.Equal(AdminPolicies.AnyStaff, Assert.Single(typeof(AdminControllerBase).GetCustomAttributes<AuthorizeAttribute>()).Policy);

        foreach (var action in actions)
        {
            var anonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() is not null || action.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            var guarded = typeof(AdminControllerBase).IsAssignableFrom(action.DeclaringType) || action.GetCustomAttributes<AuthorizeAttribute>().Any();
            Assert.True(anonymous || guarded, $"{Name(action)} neither derives from AdminControllerBase nor asks for a role.");
        }

        var open = actions
            .Where(a => a.GetCustomAttribute<AllowAnonymousAttribute>() is not null || a.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .SelectMany(a => a.GetCustomAttributes<HttpMethodAttribute>().SelectMany(h => h.HttpMethods.Where(m => m != "HEAD").Select(m => $"{Name(a)}/{m}")))
            .Order()
            .ToList();
        Assert.Equal(Anonymous.Order(), open);
    }

    [Fact]
    public void Every_admin_controller_has_the_admin_area_filter_so_no_page_is_cached_or_indexed()
    {
        var controllers = AdminActions().Select(a => a.DeclaringType!).Distinct().ToList();

        Assert.All(controllers, type => Assert.True(
            type.GetCustomAttribute<AdminAreaAttribute>(inherit: true) is not null, $"{type.Name} has no [AdminArea]."));
    }

    // The same from the other side: what the running app has registered under /admin.
    [Fact]
    public void Every_address_under_admin_belongs_to_an_admin_controller_and_carries_its_authorization()
    {
        var endpoints = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => ("/" + e.RoutePattern.RawText!.TrimStart('/')).StartsWith("/admin", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(endpoints);

        foreach (var endpoint in endpoints)
        {
            var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!;
            Assert.Equal("Peekbox.Web.Controllers.Admin", action.ControllerTypeInfo.Namespace);
            var asks = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any();
            var open = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            Assert.True(asks || open, $"{endpoint.RoutePattern.RawText} has no authorization.");
        }
    }

    // And no address outside /admin may be served by an admin controller.
    [Fact]
    public void No_admin_controller_answers_outside_the_admin_address()
    {
        var outside = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>()?.ControllerTypeInfo.Namespace == "Peekbox.Web.Controllers.Admin")
            .Where(e => !("/" + e.RoutePattern.RawText!.TrimStart('/')).StartsWith("/admin", StringComparison.Ordinal))
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        Assert.Empty(outside);
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/account")]
    [InlineData("/admin/denied")]
    public async Task Without_a_sign_in_every_guarded_page_is_a_redirect_never_a_page(string path)
    {
        var response = await fixture.Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login", AdminTestKit.Where(response));
    }

    [Theory]
    [InlineData("/admin/account/password")]
    [InlineData("/admin/account/sign-out-everywhere")]
    public async Task Without_a_sign_in_a_guarded_form_is_a_redirect_even_with_a_valid_token(string path)
    {
        var visitor = new Visitor(fixture.Client());
        await visitor.PageAsync("/admin/login");   // a real form token

        var response = await visitor.PostAsync(path, new() { ["currentPassword"] = "x", ["newPassword"] = "y", ["confirmPassword"] = "y" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/admin/login", AdminTestKit.Where(response));
    }

    // ---- who may open what ----

    private async Task<bool> AllowedAsync(string? role, string policy)
    {
        var identity = new ClaimsIdentity(role is null ? [] : [new Claim(ClaimTypes.Role, role)], authenticationType: role is null ? null : "test");
        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(new ClaimsPrincipal(identity), null, policy);
        return result.Succeeded;
    }

    [Theory]
    [InlineData(AdminRoles.Owner, AdminPolicies.AnyStaff, true)]
    [InlineData(AdminRoles.Owner, AdminPolicies.ManagerUp, true)]
    [InlineData(AdminRoles.Owner, AdminPolicies.OwnerOnly, true)]
    [InlineData(AdminRoles.Manager, AdminPolicies.AnyStaff, true)]
    [InlineData(AdminRoles.Manager, AdminPolicies.ManagerUp, true)]
    [InlineData(AdminRoles.Manager, AdminPolicies.OwnerOnly, false)]
    [InlineData(AdminRoles.Staff, AdminPolicies.AnyStaff, true)]
    [InlineData(AdminRoles.Staff, AdminPolicies.ManagerUp, false)]
    [InlineData(AdminRoles.Staff, AdminPolicies.OwnerOnly, false)]
    [InlineData(null, AdminPolicies.AnyStaff, false)]
    [InlineData(null, AdminPolicies.ManagerUp, false)]
    [InlineData(null, AdminPolicies.OwnerOnly, false)]
    [InlineData("Customer", AdminPolicies.AnyStaff, false)]   // a role name that is not ours
    [InlineData("owner", AdminPolicies.OwnerOnly, false)]     // role names are exact, not case-insensitive guesses
    public async Task Each_role_opens_only_what_it_should(string? role, string policy, bool allowed) =>
        Assert.Equal(allowed, await AllowedAsync(role, policy));

    [Fact]
    public async Task The_three_roles_are_rows_of_the_database_with_the_ids_the_code_names()
    {
        var roles = await fixture.WithDbAsync(db => db.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new { r.Id, r.Name, r.NormalizedName }).ToListAsync());

        Assert.Equal(
            [(1, "Owner", "OWNER"), (2, "Manager", "MANAGER"), (3, "Staff", "STAFF")],
            roles.Select(r => (r.Id, r.Name!, r.NormalizedName!)));
        Assert.All(AdminRoles.All, name => Assert.Equal(name, roles.Single(r => r.Id == AdminRoles.Id(name)).Name));
    }

    [Fact]
    public async Task A_staff_member_is_signed_in_with_a_staff_cookie_and_opens_the_account_screen()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture, AdminRoles.Staff, displayName: "Phạm Thu");
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);

        Assert.Contains("Phạm Thu · Nhân viên", await visitor.PageAsync("/admin/account"));
        var denied = await visitor.GetAsync("/admin/denied");   // the page shown when a role is not enough
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("Bạn không có quyền mở trang này", await visitor.TextAsync(denied));
    }
}

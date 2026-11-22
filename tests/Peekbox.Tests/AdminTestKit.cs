using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Peekbox.Web.Data;
using Peekbox.Web.Security.Admin;

namespace Peekbox.Tests;

// What the admin tests share: staff accounts made straight through the framework's UserManager (the first owner of a real
// start comes from AdminBootstrap, tested on its own), and a signed-in browser.
internal static class AdminTestKit
{
    public const string Password = "correct-horse-battery-1";

    // A new account with a name of its own, so tests do not meet. Returns the user name.
    public static async Task<string> CreateUserAsync(
        PeekboxFixture fixture, string role = AdminRoles.Owner, bool active = true, string displayName = "Nguyễn Văn An", string password = Password)
    {
        var userName = "t" + Guid.NewGuid().ToString("N")[..12];
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        var user = new AdminUser { UserName = userName, DisplayName = displayName, IsActive = active, CreatedAt = DateTimeOffset.UtcNow };
        var created = await users.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(e => e.Description)));
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return userName;
    }

    // A browser that is signed in as that account.
    public static async Task<Visitor> SignedInAsync(PeekboxFixture fixture, string userName, string password = Password, HttpClient? client = null)
    {
        var visitor = new Visitor(client ?? fixture.Client());
        await visitor.PageAsync("/admin/login");
        var response = await visitor.PostAsync("/admin/login", new() { ["userName"] = userName, ["password"] = password });
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        return visitor;
    }

    // Where a redirect goes, as a path and query: the framework's own redirect to the sign-in page is a full address, ours is not.
    public static string? Where(HttpResponseMessage response) =>
        response.Headers.Location is { } location ? (location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString) : null;

    public static Task<T> WithUserAsync<T>(PeekboxFixture fixture, string userName, Func<PeekboxDbContext, AdminUser, Task<T>> action) =>
        fixture.WithDbAsync(async db => await action(db, await db.Users.SingleAsync(u => u.UserName == userName)));
}

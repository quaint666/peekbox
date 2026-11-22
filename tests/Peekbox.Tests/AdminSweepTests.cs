using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Peekbox.Tests;

// The owner's rule (issue #105): bad input is a 400 or a message, never a 500. Every address under /admin, signed in and not,
// gets empty, malformed, oversized and odd bodies, odd methods and odd queries; nothing may answer 5xx. Also the first-owner
// setup started by several copies of the app at the same moment (the check "no user yet" then the write).
[Collection(DatabaseCollection.Name)]
public class AdminSweepTests(PeekboxFixture fixture)
{
    private static readonly string[] Fields = ["userName", "password", "returnUrl", "currentPassword", "newPassword", "confirmPassword", "code", "id"];

    private static readonly string[] OddValues =
    [
        "",
        " ",
        "\0",
        "a\0b\r\nSet-Cookie: x=1",
        "😀🧑‍🦰 العربية 日本語",
        "<script>alert(1)</script>",
        "'; DROP TABLE \"AdminUsers\"; --",
        "%00%0d%0a",
        new string('x', 200_000),
        "../../etc/passwd",
        "\\\\evil.example\\share",
        "https://evil.example/admin/x",
    ];

    private List<(string Pattern, string Method)> AdminRoutes() =>
        fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => ("/" + e.RoutePattern.RawText!.TrimStart('/')).StartsWith("/admin", StringComparison.Ordinal))
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"]).Select(m => ("/" + e.RoutePattern.RawText!.TrimStart('/'), m)))
            .Distinct()
            .ToList();

    private static IEnumerable<(string Name, Func<string, HttpRequestMessage> Build)> Requests(string token) =>
    [
        ("no body", path => new(HttpMethod.Post, path)),
        ("empty form", path => new(HttpMethod.Post, path) { Content = new FormUrlEncodedContent([]) }),
        ("garbage text", path => new(HttpMethod.Post, path) { Content = new StringContent("\u0001\u0002 not a form %%%", Encoding.UTF8, "text/plain") }),
        ("broken json", path => new(HttpMethod.Post, path) { Content = new StringContent("{\"userName\": ", Encoding.UTF8, "application/json") }),
        ("json object", path => new(HttpMethod.Post, path) { Content = new StringContent("{\"userName\":\"a\",\"password\":null}", Encoding.UTF8, "application/json") }),
        ("broken multipart", path =>
        {
            var content = new StringContent("--x\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\nb");
            content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("multipart/form-data; boundary=zzz");
            return new(HttpMethod.Post, path) { Content = content };
        }),
        ("wrong charset", path => new(HttpMethod.Post, path) { Content = new StringContent("userName=%FF%FE&password=%80", Encoding.Latin1, "application/x-www-form-urlencoded") }),
        ("repeated keys", path => new(HttpMethod.Post, path) { Content = new FormUrlEncodedContent([new("userName", "a"), new("userName", "b"), new("returnUrl", "/admin/x"), new("returnUrl", "https://evil.example"), new("__RequestVerificationToken", token)]) }),
        ("array style keys", path => new(HttpMethod.Post, path) { Content = new FormUrlEncodedContent([new("userName[]", "a"), new("password[0]", "b"), new("__RequestVerificationToken", token)]) }),
        ("odd method PUT", path => new(HttpMethod.Put, path)),
        ("odd method DELETE", path => new(HttpMethod.Delete, path)),
        ("odd method PATCH", path => new(HttpMethod.Patch, path) { Content = new StringContent("x") }),
        ("odd method OPTIONS", path => new(HttpMethod.Options, path)),
        ("odd query", path => new(HttpMethod.Get, path + "?returnUrl[]=x&returnUrl=%00&returnUrl=" + Uri.EscapeDataString(new string('y', 50_000)) + "&a=%FF")),
        ("odd path tail", path => new(HttpMethod.Get, path + "/..;/x%20y/%2e%2e/%E0%A4%A")),
    ];

    private async Task SweepAsync(Visitor visitor, string token, string who)
    {
        var failures = new List<string>();

        async Task Check(string what, HttpRequestMessage request)
        {
            var response = await visitor.SendRawAsync(request);
            if ((int)response.StatusCode >= 500)
            {
                failures.Add($"{who}: {what} gave {(int)response.StatusCode}");
            }
        }

        foreach (var (pattern, _) in AdminRoutes().Where(r => r.Method is "GET" or "POST"))
        {
            foreach (var (name, build) in Requests(token))
            {
                await Check($"{name} on {pattern}", build(pattern));
            }
        }

        // A valid token with every odd value in every field, on every POST address.
        foreach (var pattern in AdminRoutes().Where(r => r.Method == "POST").Select(r => r.Pattern).Distinct())
        {
            foreach (var value in OddValues)
            {
                var form = Fields.Select(f => new KeyValuePair<string, string>(f, value)).Append(new("__RequestVerificationToken", token));
                await Check($"odd value ({(value.Length > 20 ? value[..20] + "…" : value).Replace("\0", "\\0")}) on {pattern}", new(HttpMethod.Post, pattern) { Content = new FormUrlEncodedContent(form) });
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(20)));
    }

    [Fact]
    public async Task Nobody_signed_in_can_make_any_admin_address_fail_with_a_500()
    {
        var visitor = new Visitor(fixture.Client());
        await visitor.PageAsync("/admin/login");

        await SweepAsync(visitor, visitor.FormToken, "anonymous");
    }

    [Fact]
    public async Task A_signed_in_owner_cannot_make_any_admin_address_fail_with_a_500_either()
    {
        var visitor = await AdminTestKit.SignedInAsync(fixture, await AdminTestKit.CreateUserAsync(fixture));
        await visitor.PageAsync("/admin/account");

        // The sweep posts to every address, "Đăng xuất" and the password form included, so it ends the session and wrong current
        // passwords lock the account: only a 5xx counts as a failure here.
        await SweepAsync(visitor, visitor.FormToken, "owner");
    }

    // ---- the first owner, started by several copies at the same moment ----

    [Fact]
    public async Task Several_copies_of_the_app_starting_at_the_same_moment_make_exactly_one_first_owner_and_none_fails()
    {
        var connection = await fixture.NewMigratedDatabaseAsync();
        var apps = Enumerable.Range(0, 5).Select(_ => fixture.Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Default", connection);
            b.UseSetting("Admin:OwnerUserName", "chu-cua-hang");
            b.UseSetting("Admin:OwnerPassword", "a-long-first-owner-password");
            b.UseSetting("Admin:OwnerDisplayName", "An");
        })).ToList();
        try
        {
            await Task.WhenAll(apps.Select(app => Task.Run(() => app.CreateClient())));   // a copy that failed to start would throw here

            using var scope = apps[0].Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Peekbox.Web.Data.PeekboxDbContext>();
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal(1, await db.UserRoles.CountAsync());
        }
        finally
        {
            foreach (var app in apps)
            {
                await app.DisposeAsync();
            }
        }
    }
}

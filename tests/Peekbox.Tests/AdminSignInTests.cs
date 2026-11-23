using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Security.Admin;

namespace Peekbox.Tests;

// Signing in to the admin (F56): the page, the wrong tries and what they cost, the lockout, the cookie, the account page, and
// the rules that the person's row in the database is read on every request. Every test makes its own account.
[Collection(DatabaseCollection.Name)]
public class AdminSignInTests(PeekboxFixture fixture)
{
    private const string Generic = "Tên đăng nhập hoặc mật khẩu không đúng.";

    private async Task<(HttpStatusCode Status, string Html, Visitor Visitor)> AttemptAsync(string userName, string password, HttpClient? client = null)
    {
        var visitor = new Visitor(client ?? fixture.Client());
        await visitor.PageAsync("/admin/login");
        var response = await visitor.PostAsync("/admin/login", new() { ["userName"] = userName, ["password"] = password });
        return (response.StatusCode, response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.TooManyRequests ? await visitor.TextAsync(response) : "", visitor);
    }

    // ---- the page ----

    [Fact]
    public async Task The_sign_in_page_has_a_form_with_the_two_fields_and_no_second_step_and_is_never_cached_or_indexed()
    {
        var response = await fixture.Client().GetAsync("/admin/login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single());
        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\">", html);
        Assert.Contains("<html lang=\"vi\" data-theme=\"light\">", html);
        Assert.Contains("<title>Đăng nhập · Quản trị · Peekbox</title>", WebUtility.HtmlDecode(html));
        Assert.Contains("for=\"user-name\"", html);
        Assert.Contains("autocomplete=\"username\"", html);
        Assert.Contains("type=\"password\" autocomplete=\"current-password\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        // The second step is not built: no field that would accept any digits, no recovery-code link.
        Assert.DoesNotContain("2 bước", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("name=\"code\"", html);
        Assert.DoesNotContain("theme-init.js", html);   // the shop's theme script is not loaded: the admin is light only
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/account")]
    [InlineData("/admin/denied")]
    public async Task A_screen_without_a_sign_in_goes_to_the_sign_in_page_and_remembers_where_it_was_going(string path)
    {
        var response = await fixture.Client().GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/admin/login?returnUrl={Uri.EscapeDataString(path)}", AdminTestKit.Where(response));
    }

    // ---- wrong tries ----

    [Fact]
    public async Task A_wrong_password_an_unknown_name_and_a_switched_off_account_all_get_the_same_answer_and_no_cookie()
    {
        var real = await AdminTestKit.CreateUserAsync(fixture);
        var off = await AdminTestKit.CreateUserAsync(fixture, active: false);

        var wrong = await AttemptAsync(real, "not-the-password");
        var unknown = await AttemptAsync("nobody-" + Guid.NewGuid().ToString("N")[..8], "not-the-password");
        var switchedOff = await AttemptAsync(off, AdminTestKit.Password);   // the right password of a switched-off account

        foreach (var attempt in new[] { wrong, unknown, switchedOff })
        {
            Assert.Equal(HttpStatusCode.OK, attempt.Status);
            Assert.Contains(Generic, attempt.Html);
            Assert.DoesNotContain("Peekbox.Admin", string.Join(";", attempt.Visitor.LastSetCookies));
        }

        // What was typed as the name stays; the password never comes back.
        Assert.Contains($"value=\"{real}\"", wrong.Html);
        Assert.DoesNotContain("not-the-password", wrong.Html);
    }

    [Fact]
    public async Task Empty_and_far_too_long_fields_are_answered_not_failed()
    {
        var empty = await AttemptAsync("", "");
        var hugeName = await AttemptAsync(new string('a', 10_000), "x");
        var hugePassword = await AttemptAsync("someone", new string('p', 100_000));

        Assert.Contains("Nhập tên đăng nhập và mật khẩu.", empty.Html);
        Assert.Contains(Generic, hugeName.Html);
        Assert.Contains(Generic, hugePassword.Html);
        Assert.DoesNotContain(new string('a', 100), hugeName.Html);   // a long text is not echoed back
        Assert.All(new[] { empty, hugeName, hugePassword }, a => Assert.Equal(HttpStatusCode.OK, a.Status));
    }

    // ---- the lockout ----

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_15_minutes_and_even_the_right_one_is_then_refused()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        for (var i = 1; i <= 4; i++)
        {
            Assert.Contains(Generic, (await AttemptAsync(name, "wrong-" + i)).Html);
        }

        var fifth = await AttemptAsync(name, "wrong-5");
        var locked = WebUtility.HtmlDecode(fifth.Html);

        Assert.Matches(@"Đã nhập sai 5 lần\. Vì an toàn, đăng nhập bị khóa 15 phút\. Thử lại sau \d\d:\d\d\.", locked);
        Assert.DoesNotContain(Generic, locked);
        Assert.Contains("class=\"btn btn--primary admin-login__submit\" disabled", fifth.Html);   // the button is off, as in state 15
        var until = (await AdminTestKit.WithUserAsync(fixture, name, (_, u) => Task.FromResult(u.LockoutEnd)))!.Value;
        Assert.InRange(until - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.1));

        var right = await AttemptAsync(name, AdminTestKit.Password);
        Assert.Equal(HttpStatusCode.OK, right.Status);
        Assert.Contains("đăng nhập bị khóa 15 phút", WebUtility.HtmlDecode(right.Html));
        Assert.DoesNotContain("Peekbox.Admin", string.Join(";", right.Visitor.LastSetCookies));
    }

    [Fact]
    public async Task A_good_password_starts_the_count_of_wrong_ones_again()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        for (var i = 0; i < 3; i++)
        {
            await AttemptAsync(name, "wrong");
        }

        Assert.Equal(3, await AdminTestKit.WithUserAsync(fixture, name, (_, u) => Task.FromResult(u.AccessFailedCount)));
        Assert.Equal(HttpStatusCode.Redirect, (await AttemptAsync(name, AdminTestKit.Password)).Status);
        Assert.Equal(0, await AdminTestKit.WithUserAsync(fixture, name, (_, u) => Task.FromResult(u.AccessFailedCount)));
    }

    // Guesses sent at the same moment must all count. The framework's own counter reads, adds one and writes back with a stamp,
    // so most of them were lost; here each is one UPDATE. Seen to fail with the framework's counter.
    [Fact]
    public async Task Guesses_at_the_same_moment_are_all_counted()
    {
        var four = await AdminTestKit.CreateUserAsync(fixture);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AttemptAsync(four, "wrong")));
        Assert.Equal(4, await AdminTestKit.WithUserAsync(fixture, four, (_, u) => Task.FromResult(u.AccessFailedCount)));   // none lost, not yet locked

        var many = await AdminTestKit.CreateUserAsync(fixture);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => AttemptAsync(many, "wrong")));
        Assert.True((await AdminTestKit.WithUserAsync(fixture, many, (_, u) => Task.FromResult(u.LockoutEnd))) > DateTimeOffset.UtcNow);
        Assert.Contains("khóa", WebUtility.HtmlDecode((await AttemptAsync(many, AdminTestKit.Password)).Html));
    }

    // ---- the address's limit ----

    [Fact]
    public async Task One_address_may_fail_only_so_often_and_good_sign_ins_do_not_count()
    {
        await using var limited = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:AdminLoginFailuresPer15Minutes", "2"));
        var client = fixture.Client(limited);
        var name = await AdminTestKit.CreateUserAsync(fixture);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await AttemptAsync(name, AdminTestKit.Password, client)).Status);   // good ones are free
        }

        Assert.Equal(HttpStatusCode.OK, (await AttemptAsync(name, "wrong", client)).Status);
        Assert.Equal(HttpStatusCode.OK, (await AttemptAsync(name, "wrong", client)).Status);
        var refused = await AttemptAsync(name, AdminTestKit.Password, client);   // the address is out of tries: the right password does not help

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.Status);
        Assert.Contains("Bạn đã thử quá nhiều lần.", refused.Html);
    }

    // ---- signing in ----

    [Fact]
    public async Task A_good_sign_in_gives_a_session_cookie_that_is_httponly_and_strict_and_opens_the_account_page()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture, AdminRoles.Manager, displayName: "Nguyễn Văn An");
        var visitor = new Visitor(fixture.Client());
        await visitor.PageAsync("/admin/login");

        var response = await visitor.PostAsync("/admin/login", new() { ["userName"] = name.ToUpperInvariant(), ["password"] = AdminTestKit.Password });   // the name is not case sensitive

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/orders", response.Headers.Location?.OriginalString);
        var cookie = Assert.Single(visitor.LastSetCookies, c => c.StartsWith("Peekbox.Admin=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);   // a session cookie: no "remember me"
        Assert.DoesNotContain("max-age", cookie, StringComparison.OrdinalIgnoreCase);

        var page = await visitor.PageAsync("/admin/account");
        Assert.Contains("<h1 class=\"admin-title\">Tài khoản của tôi</h1>", page);
        Assert.Contains("Nguyễn Văn An · Quản lý", page);
        Assert.Contains("<span class=\"admin-chip__initials\" aria-hidden=\"true\">NA</span>", page);
        Assert.Contains("<span class=\"admin-chip__role\">Quản lý</span>", page);
        Assert.Contains("Ít nhất 10 ký tự.", page);   // the hint comes from the rule's own number
        Assert.Contains("Xác thực 2 bước", page);
        Assert.Contains("Chưa dùng được.", page);   // the 2-step code is said not to exist yet
        var raw = await visitor.GetAsync("/admin/account");
        Assert.Contains("no-store", raw.Headers.CacheControl?.ToString());
        Assert.Equal("noindex, nofollow", raw.Headers.GetValues("X-Robots-Tag").Single());
    }

    // Behind the proxy the app sees http; ForwardedHeaders makes the request https again, and then the cookie is Secure.
    [Fact]
    public async Task The_cookie_is_secure_when_the_proxy_says_the_request_was_https_and_not_on_plain_http()
    {
        await using var proxied = fixture.Factory.WithWebHostBuilder(b => b.UseSetting("ForwardedHeaders:Hops", "1"));
        var name = await AdminTestKit.CreateUserAsync(fixture);

        async Task<string> CookieAsync(bool https)
        {
            var client = fixture.Client(proxied);
            var page = new HttpRequestMessage(HttpMethod.Get, "/admin/login");
            if (https) { page.Headers.Add("X-Forwarded-Proto", "https"); }
            var loginPage = await client.SendAsync(page);
            var raw = await loginPage.Content.ReadAsStringAsync();
            var token = System.Text.RegularExpressions.Regex.Match(raw, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
            var antiforgery = loginPage.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal)).Split(';')[0];
            var post = new HttpRequestMessage(HttpMethod.Post, "/admin/login")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["userName"] = name, ["password"] = AdminTestKit.Password, ["__RequestVerificationToken"] = token }),
            };
            post.Headers.Add("Cookie", antiforgery);
            if (https) { post.Headers.Add("X-Forwarded-Proto", "https"); }
            var response = await client.SendAsync(post);
            return response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("Peekbox.Admin=", StringComparison.Ordinal));
        }

        Assert.Contains("secure", await CookieAsync(https: true), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", await CookieAsync(https: false), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/admin/account", "/admin/account")]
    [InlineData("/admin/orders", "/admin/orders")]
    [InlineData("https://evil.example/admin/x", "/admin/orders")]
    [InlineData("//evil.example/admin/x", "/admin/orders")]
    [InlineData("/builder", "/admin/orders")]
    [InlineData("/adminx", "/admin/orders")]
    [InlineData("", "/admin/orders")]
    public async Task After_signing_in_the_person_goes_on_only_to_an_address_inside_the_admin(string returnUrl, string expected)
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var visitor = new Visitor(fixture.Client());
        await visitor.PageAsync("/admin/login");

        var response = await visitor.PostAsync("/admin/login", new() { ["userName"] = name, ["password"] = AdminTestKit.Password, ["returnUrl"] = returnUrl });

        Assert.Equal(expected, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Someone_already_signed_in_is_taken_from_the_sign_in_page_to_the_orders()
    {
        var visitor = await AdminTestKit.SignedInAsync(fixture, await AdminTestKit.CreateUserAsync(fixture));

        var response = await visitor.GetAsync("/admin/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/orders", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Signing_out_ends_the_session_and_says_so()
    {
        var visitor = await AdminTestKit.SignedInAsync(fixture, await AdminTestKit.CreateUserAsync(fixture));
        await visitor.PageAsync("/admin/account");

        var response = await visitor.PostAsync("/admin/logout", new());

        Assert.Equal("/admin/login", response.Headers.Location?.OriginalString);
        Assert.Contains("Bạn đã đăng xuất.", await visitor.PageAsync("/admin/login"));
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.GetAsync("/admin/account")).StatusCode);
    }

    // ---- the row is read on every request ----

    [Fact]
    public async Task A_person_switched_off_is_signed_out_at_the_next_request()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/admin/account")).StatusCode);

        await AdminTestKit.WithUserAsync(fixture, name, async (db, u) => { u.IsActive = false; return await db.SaveChangesAsync(); });

        var after = await visitor.GetAsync("/admin/account");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.StartsWith("/admin/login", AdminTestKit.Where(after));
    }

    [Fact]
    public async Task A_changed_role_and_a_changed_name_show_at_the_next_request_without_signing_in_again()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture, AdminRoles.Owner, displayName: "Trần Thị Bé");
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);
        Assert.Contains("Trần Thị Bé · Chủ cửa hàng", await visitor.PageAsync("/admin/account"));

        await AdminTestKit.WithUserAsync(fixture, name, async (db, u) =>
        {
            u.DisplayName = "Lê Văn Cường";
            var row = await db.UserRoles.SingleAsync(r => r.UserId == u.Id);
            db.UserRoles.Remove(row);
            db.UserRoles.Add(new() { UserId = u.Id, RoleId = AdminRoles.Id(AdminRoles.Staff) });
            return await db.SaveChangesAsync();
        });

        var page = await visitor.PageAsync("/admin/account");
        Assert.Contains("Lê Văn Cường · Nhân viên", page);
        Assert.Contains(">LC</span>", page);
    }

    [Fact]
    public async Task A_person_with_no_role_at_all_gets_nothing()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);

        await AdminTestKit.WithUserAsync(fixture, name, async (db, u) => await db.UserRoles.Where(r => r.UserId == u.Id).ExecuteDeleteAsync());

        var response = await visitor.GetAsync("/admin/account");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);   // signed in, but not allowed: sent to the denied page
        Assert.StartsWith("/admin/denied", AdminTestKit.Where(response));
    }

    [Fact]
    public async Task A_locked_account_with_an_open_session_is_signed_out()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);

        await AdminTestKit.WithUserAsync(fixture, name, async (db, u) => { u.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10); return await db.SaveChangesAsync(); });

        Assert.StartsWith("/admin/login", AdminTestKit.Where(await visitor.GetAsync("/admin/account")));
    }

    // ---- the password ----

    [Fact]
    public async Task Changing_the_password_keeps_this_session_ends_the_others_and_the_new_one_works()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var here = await AdminTestKit.SignedInAsync(fixture, name);
        var elsewhere = await AdminTestKit.SignedInAsync(fixture, name);
        await here.PageAsync("/admin/account");

        var response = await here.PostAsync("/admin/account/password", new() { ["currentPassword"] = AdminTestKit.Password, ["newPassword"] = "a-brand-new-passphrase", ["confirmPassword"] = "a-brand-new-passphrase" });

        Assert.Equal("/admin/account", response.Headers.Location?.OriginalString);
        Assert.Contains("Đã đổi mật khẩu.", await here.PageAsync("/admin/account"));
        Assert.Equal(HttpStatusCode.OK, (await here.GetAsync("/admin/account")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await elsewhere.GetAsync("/admin/account")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AttemptAsync(name, AdminTestKit.Password)).Status);                  // the old password is refused
        Assert.Equal(HttpStatusCode.Redirect, (await AttemptAsync(name, "a-brand-new-passphrase")).Status);
    }

    [Theory]
    [InlineData(AdminTestKit.Password, "short", "short", "Mật khẩu cần ít nhất 10 ký tự.")]
    [InlineData(AdminTestKit.Password, "aaaaaaaaaaaa", "aaaaaaaaaaaa", "Mật khẩu cần ít nhất 4 ký tự khác nhau.")]
    [InlineData(AdminTestKit.Password, "a-brand-new-passphrase", "another-passphrase", "Hai mật khẩu mới không giống nhau.")]
    [InlineData(AdminTestKit.Password, AdminTestKit.Password, AdminTestKit.Password, "Mật khẩu mới phải khác mật khẩu hiện tại.")]
    [InlineData("", "a-brand-new-passphrase", "a-brand-new-passphrase", "Nhập mật khẩu hiện tại và mật khẩu mới hai lần.")]
    [InlineData("not-my-password", "a-brand-new-passphrase", "a-brand-new-passphrase", "Mật khẩu hiện tại không đúng.")]
    public async Task A_password_change_that_cannot_be_made_says_why_in_Vietnamese_and_changes_nothing(string current, string next, string confirm, string message)
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);
        await visitor.PageAsync("/admin/account");

        var response = await visitor.PostAsync("/admin/account/password", new() { ["currentPassword"] = current, ["newPassword"] = next, ["confirmPassword"] = confirm });
        var html = await visitor.TextAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, html);
        Assert.Contains("role=\"alert\"", html);
        Assert.Equal(HttpStatusCode.Redirect, (await AttemptAsync(name, AdminTestKit.Password)).Status);   // still the old one
    }

    [Fact]
    public async Task Guessing_the_current_password_in_the_change_form_locks_the_account_after_five_tries()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);
        await visitor.PageAsync("/admin/account");

        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await visitor.PostAsync("/admin/account/password", new() { ["currentPassword"] = "guess-" + i, ["newPassword"] = "a-brand-new-passphrase", ["confirmPassword"] = "a-brand-new-passphrase" });
        }

        Assert.Equal("/admin/login", last.Headers.Location?.OriginalString);   // the fifth wrong one signs the person out
        Assert.True((await AdminTestKit.WithUserAsync(fixture, name, (_, u) => Task.FromResult(u.LockoutEnd))) > DateTimeOffset.UtcNow);
        Assert.StartsWith("/admin/login", AdminTestKit.Where(await visitor.GetAsync("/admin/account")));
    }

    [Fact]
    public async Task Sign_out_of_every_device_ends_all_sessions_including_this_one()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var here = await AdminTestKit.SignedInAsync(fixture, name);
        var elsewhere = await AdminTestKit.SignedInAsync(fixture, name);
        await here.PageAsync("/admin/account");

        var response = await here.PostAsync("/admin/account/sign-out-everywhere", new());

        Assert.Equal("/admin/login", response.Headers.Location?.OriginalString);
        Assert.Contains("Đã đăng xuất khỏi mọi thiết bị.", await here.PageAsync("/admin/login"));
        Assert.Equal(HttpStatusCode.Redirect, (await here.GetAsync("/admin/account")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await elsewhere.GetAsync("/admin/account")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await AttemptAsync(name, AdminTestKit.Password)).Status);   // the account itself still works
    }

    [Fact]
    public async Task A_password_over_the_longest_allowed_is_refused_before_it_is_hashed()
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);
        await visitor.PageAsync("/admin/account");
        var huge = new string('x', 100_000);

        var response = await visitor.PostAsync("/admin/account/password", new() { ["currentPassword"] = AdminTestKit.Password, ["newPassword"] = huge, ["confirmPassword"] = huge });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Mật khẩu dài tối đa 128 ký tự.", await visitor.TextAsync(response));
    }

    [Theory]
    [InlineData("/admin/account/password")]
    [InlineData("/admin/account/sign-out-everywhere")]
    [InlineData("/admin/logout")]
    public async Task A_signed_in_post_without_the_form_token_is_refused_and_changes_nothing(string path)
    {
        var name = await AdminTestKit.CreateUserAsync(fixture);
        var visitor = await AdminTestKit.SignedInAsync(fixture, name);
        await visitor.PageAsync("/admin/account");

        var response = await visitor.PostAsync(path, new() { ["currentPassword"] = AdminTestKit.Password, ["newPassword"] = "a-brand-new-passphrase", ["confirmPassword"] = "a-brand-new-passphrase" }, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/admin/account")).StatusCode);   // still signed in
        Assert.Equal(HttpStatusCode.Redirect, (await AttemptAsync(name, AdminTestKit.Password)).Status);   // the password is unchanged
    }

    [Fact]
    public async Task The_change_form_has_a_hidden_user_name_for_password_managers_and_no_script()
    {
        var visitor = await AdminTestKit.SignedInAsync(fixture, await AdminTestKit.CreateUserAsync(fixture));

        var raw = await (await visitor.GetAsync("/admin/account")).Content.ReadAsStringAsync();

        Assert.Matches("<input type=\"text\" class=\"visually-hidden\" value=\"t[0-9a-f]{12}\" autocomplete=\"username\"", raw);
        Assert.DoesNotContain("<script", raw);
        Assert.DoesNotContain(" style=\"", raw);
    }
}

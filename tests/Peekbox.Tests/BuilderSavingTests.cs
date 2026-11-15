using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

[Collection(DatabaseCollection.Name)]
public class BuilderSavingTests(PeekboxFixture fixture)
{
    // A visitor with a cookie jar that the test controls (so it can also send a forged cookie).
    private sealed class Visitor(HttpClient client)
    {
        public Dictionary<string, string> Cookies { get; } = [];

        public string? Token => Cookies.GetValueOrDefault(HamperService.CookieName);

        public List<string> LastSetCookies { get; private set; } = [];

        public async Task<HttpResponseMessage> GetAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync(request);
        }

        // Opens /builder first (to get the anti-forgery cookie and token), then posts the fields.
        public async Task<HttpResponseMessage> PostAsync(Dictionary<string, string> fields, bool withAntiForgeryToken = true)
        {
            var page = WebUtility.HtmlDecode(await (await GetAsync("/builder")).Content.ReadAsStringAsync());
            var form = new Dictionary<string, string>(fields);
            if (withAntiForgeryToken)
            {
                form["__RequestVerificationToken"] = Regex.Match(page, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "/builder") { Content = new FormUrlEncodedContent(form) };
            return await SendAsync(request);
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            if (Cookies.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(c => $"{c.Key}={c.Value}")));
            }

            var response = await client.SendAsync(request);
            LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
            foreach (var setCookie in LastSetCookies)
            {
                var pair = setCookie.Split(';')[0].Split('=', 2);
                Cookies[pair[0]] = pair[1];
            }

            return response;
        }
    }

    private Visitor NewVisitor() =>
        new(fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false }));

    private static DateOnly TodayInVietnam => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    private Task<int> CountHampersAsync() => fixture.WithDbAsync(db => db.Hampers.CountAsync());

    private Task<Hamper> HamperAsync(string token) => fixture.WithDbAsync(db => db.Hampers.SingleAsync(h => h.Token == token));

    private static async Task<string> BodyAsync(HttpResponseMessage response) => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task A_good_budget_and_date_are_saved_and_the_visitor_goes_back_to_the_builder()
    {
        var visitor = NewVisitor();
        var date = TodayInVietnam.AddDays(30);

        var response = await visitor.PostAsync(new() { ["budget"] = "500.000đ", ["deliveryDate"] = Iso(date) });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/builder", response.Headers.Location?.OriginalString);
        Assert.NotNull(visitor.Token);
        var hamper = await HamperAsync(visitor.Token!);
        Assert.Equal(500_000, hamper.BudgetVnd);
        Assert.Equal(date, hamper.DeliveryDate);

        // Back on the page, the fields show what was saved.
        var html = await BodyAsync(await visitor.GetAsync("/builder"));
        Assert.Contains("id=\"budget\"", html);
        Assert.Contains("value=\"500.000đ\"", html);
        Assert.Contains($"value=\"{Iso(date)}\"", html);
        Assert.DoesNotContain("field__error", html);
    }

    [Fact]
    public async Task The_cookie_is_named_peekbox_hamper_lasts_180_days_and_is_http_only_and_same_site_lax()
    {
        var visitor = NewVisitor();

        await visitor.PostAsync(new() { ["budget"] = "300000" });

        var setCookie = Assert.Single(visitor.LastSetCookies, c => c.StartsWith("peekbox_hamper=")).ToLowerInvariant();
        Assert.Contains("httponly", setCookie);
        Assert.Contains("samesite=lax", setCookie);
        Assert.Contains($"max-age={180 * 24 * 60 * 60}", setCookie);
        Assert.DoesNotContain("secure", setCookie);   // the test site runs on plain http; "Secure" is only added on https
    }

    [Fact]
    public async Task The_cookie_is_a_long_random_code_that_differs_for_every_visitor()
    {
        var first = NewVisitor();
        var second = NewVisitor();

        await first.PostAsync(new() { ["budget"] = "300000" });
        await second.PostAsync(new() { ["budget"] = "300000" });

        Assert.True(first.Token!.Length >= 40, "the code should be long");
        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public async Task A_quick_amount_is_saved_instead_of_what_is_typed_and_saves_a_typed_date_too()
    {
        var visitor = NewVisitor();
        var date = TodayInVietnam.AddDays(10);

        var response = await visitor.PostAsync(new() { ["budget"] = "123", ["quickAmount"] = "800000", ["deliveryDate"] = Iso(date) });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var hamper = await HamperAsync(visitor.Token!);
        Assert.Equal(800_000, hamper.BudgetVnd);
        Assert.Equal(date, hamper.DeliveryDate);
    }

    [Fact]
    public async Task The_date_can_be_left_empty()
    {
        var visitor = NewVisitor();

        var response = await visitor.PostAsync(new() { ["budget"] = "500000", ["deliveryDate"] = "" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null((await HamperAsync(visitor.Token!)).DeliveryDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("500,000")]
    [InlineData("99999999999999999999")]
    public async Task A_bad_budget_shows_a_warning_keeps_what_was_typed_and_saves_nothing(string typed)
    {
        var visitor = NewVisitor();
        var before = await CountHampersAsync();

        var response = await visitor.PostAsync(new() { ["budget"] = typed, ["deliveryDate"] = Iso(TodayInVietnam.AddDays(10)) });
        var html = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<p class=\"field__error\" id=\"budget-error\">Nhập ngân sách bằng số, ví dụ 500.000đ.</p>", html);
        Assert.Contains("aria-invalid=\"true\"", html);
        Assert.Contains($"value=\"{typed}\"", html);
        Assert.Contains($"value=\"{Iso(TodayInVietnam.AddDays(10))}\"", html);   // the good date is kept as well
        Assert.Null(visitor.Token);
        Assert.Equal(before, await CountHampersAsync());
    }

    [Theory]
    [InlineData(-1, "Ngày giao hàng sớm nhất là ngày mai.")]     // yesterday
    [InlineData(0, "Ngày giao hàng sớm nhất là ngày mai.")]      // today
    [InlineData(367, "Ngày giao hàng muộn nhất là sau 1 năm kể từ hôm nay.")]
    public async Task A_date_outside_the_limits_shows_a_warning_keeps_what_was_typed_and_saves_nothing(int daysFromToday, string message)
    {
        var visitor = NewVisitor();
        var before = await CountHampersAsync();
        var typed = Iso(TodayInVietnam.AddDays(daysFromToday));

        var response = await visitor.PostAsync(new() { ["budget"] = "500000", ["deliveryDate"] = typed });
        var html = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"<p class=\"field__error\" id=\"delivery-date-error\">{message}</p>", html);
        Assert.Contains($"value=\"{typed}\"", html);
        Assert.DoesNotContain("budget-error", html);
        Assert.Equal(before, await CountHampersAsync());
    }

    [Fact]
    public async Task A_date_that_cannot_be_read_shows_the_invalid_date_warning()
    {
        var visitor = NewVisitor();

        var html = await BodyAsync(await visitor.PostAsync(new() { ["budget"] = "500000", ["deliveryDate"] = "15/01/2027" }));

        Assert.Contains("<p class=\"field__error\" id=\"delivery-date-error\">Ngày giao hàng không hợp lệ.</p>", html);
    }

    [Fact]
    public async Task Both_problems_show_both_warnings()
    {
        var visitor = NewVisitor();

        var html = await BodyAsync(await visitor.PostAsync(new() { ["budget"] = "abc", ["deliveryDate"] = Iso(TodayInVietnam) }));

        Assert.Contains("id=\"budget-error\"", html);
        Assert.Contains("id=\"delivery-date-error\"", html);
    }

    [Fact]
    public async Task A_second_save_with_the_same_cookie_updates_the_same_row()
    {
        var visitor = NewVisitor();
        await visitor.PostAsync(new() { ["budget"] = "300000" });
        var token = visitor.Token!;
        var after_first = await CountHampersAsync();

        await visitor.PostAsync(new() { ["budget"] = "1.000.000đ" });

        Assert.Equal(token, visitor.Token);
        Assert.Equal(after_first, await CountHampersAsync());
        var hamper = await HamperAsync(token);
        Assert.Equal(1_000_000, hamper.BudgetVnd);
        Assert.True(hamper.UpdatedAt >= hamper.CreatedAt);
    }

    [Fact]
    public async Task A_cookie_that_names_no_row_is_ignored_and_never_becomes_the_token_of_a_new_row()
    {
        var visitor = NewVisitor();
        visitor.Cookies[HamperService.CookieName] = "forged-token-that-the-server-never-made";

        var emptyPage = await BodyAsync(await visitor.GetAsync("/builder"));
        await visitor.PostAsync(new() { ["budget"] = "500000" });

        Assert.Contains("id=\"budget\" name=\"budget\" type=\"text\" inputmode=\"numeric\" autocomplete=\"off\" value=\"\"", emptyPage);
        Assert.NotEqual("forged-token-that-the-server-never-made", visitor.Token);
        Assert.Equal(500_000, (await HamperAsync(visitor.Token!)).BudgetVnd);
        Assert.False(await fixture.WithDbAsync(db => db.Hampers.AnyAsync(h => h.Token == "forged-token-that-the-server-never-made")));
    }

    [Fact]
    public async Task A_very_long_cookie_value_does_not_break_the_page()
    {
        var visitor = NewVisitor();
        visitor.Cookies[HamperService.CookieName] = new string('x', 2000);

        var response = await visitor.GetAsync("/builder");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_post_without_the_anti_forgery_token_is_refused_and_saves_nothing()
    {
        var visitor = NewVisitor();
        var before = await CountHampersAsync();

        var response = await visitor.PostAsync(new() { ["budget"] = "500000" }, withAntiForgeryToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await CountHampersAsync());
    }

    [Fact]
    public async Task A_visitor_with_no_cookie_sees_empty_fields()
    {
        var html = await BodyAsync(await NewVisitor().GetAsync("/builder"));

        Assert.Contains("id=\"budget\" name=\"budget\" type=\"text\" inputmode=\"numeric\" autocomplete=\"off\" value=\"\"", html);
        Assert.Contains("id=\"delivery-date\" name=\"deliveryDate\" type=\"date\" value=\"\"", html);
    }
}

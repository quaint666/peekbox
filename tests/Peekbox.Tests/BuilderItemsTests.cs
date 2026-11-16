using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// The item list of /builder (F16, F18, F22, F23) with the 24 seed items. The expected names and counts are written out
// on purpose: a test that computed them with the same rule would agree with a mistake in it.
[Collection(DatabaseCollection.Name)]
public class BuilderItemsTests(PeekboxFixture fixture)
{
    // Delivery 15/01/2027: with the 7-day margin, Bánh quy bơ (10/01/2027) and Kẹo lạc (18/01/2027) are out of date.
    private static readonly DateOnly Delivery = new(2027, 1, 15);

    private HttpClient Client() => fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    // A visitor who already pressed "Lưu": a hamper row, found by its cookie.
    private async Task<string> SavedHamperAsync(DateOnly? deliveryDate)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(db =>
        {
            db.Hampers.Add(new Hamper { Token = token, BudgetVnd = 500_000, DeliveryDate = deliveryDate, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });
        return token;
    }

    private async Task<string> PageAsync(string? token, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/builder" + query);
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"{HamperService.CookieName}={token}");
        }

        var response = await Client().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static List<string> CardNames(string html) =>
        Regex.Matches(html, @"<h3 class=""item-card__name"">([^<]*)</h3>").Select(m => m.Groups[1].Value).ToList();

    private static string CurrentStep(string html) =>
        Regex.Match(html, @"<li class=""builder-step"" aria-current=""step""><span class=""builder-step__num"">(\d)</span>").Groups[1].Value;

    [Fact]
    public async Task Without_a_saved_hamper_only_the_lock_note_shows_and_step_1_is_current()
    {
        var html = await PageAsync(token: null);

        Assert.Contains("Nhập ngân sách và ngày giao hàng rồi bấm <strong>Lưu</strong> để mở bước chọn món và đóng gói.", html);
        Assert.Empty(CardNames(html));
        Assert.DoesNotContain("item-filters", html);
        Assert.Equal("1", CurrentStep(html));
    }

    [Fact]
    public async Task A_saved_budget_without_a_date_keeps_the_items_locked()
    {
        var html = await PageAsync(await SavedHamperAsync(deliveryDate: null));

        Assert.Contains("class=\"lock-note\"", html);
        Assert.Empty(CardNames(html));
    }

    [Fact]
    public async Task With_a_saved_date_the_items_in_date_show_and_the_two_out_of_date_ones_are_hidden()
    {
        var html = await PageAsync(await SavedHamperAsync(Delivery));
        var names = CardNames(html);

        Assert.DoesNotContain("class=\"lock-note\"", html);
        Assert.Equal(22, names.Count);
        Assert.DoesNotContain("Bánh quy bơ", names);
        Assert.DoesNotContain("Kẹo lạc", names);
        Assert.Contains("Hiện 22 món. 2 món hết hạn trước ngày giao đang được ẩn.", html);
        Assert.Matches(@"<a href=""/builder\?inDate=false#chon-mon"">Hiện cả 2 món</a>", html);
        Assert.Contains("Chỉ món đủ hạn cho ngày giao <strong>15/01/2027</strong>", html);
        Assert.Contains("HSD 12/2027 · đủ hạn", html);   // Trà sen, 31/12/2027
        Assert.Equal("2", CurrentStep(html));
        Assert.Contains("<li class=\"builder-step builder-step--done\"><span class=\"builder-step__num\">1</span>", html);
    }

    [Fact]
    public async Task Turning_the_checkbox_off_shows_all_24_with_an_amber_badge_on_the_two()
    {
        var token = await SavedHamperAsync(Delivery);
        var html = await PageAsync(token, "?inDate=false");

        Assert.Equal(24, CardNames(html).Count);
        Assert.Contains("Hiện 24 món.", html);
        Assert.DoesNotContain("đang được ẩn", html);
        Assert.Contains("HSD 10/01/2027, trước ngày giao", html);
        Assert.Contains("HSD 18/01/2027, trước ngày giao", html);
        Assert.DoesNotMatch(@"<input[^>]*id=""in-date""[^>]*checked", html);

        // A ticked checkbox sends "true" and its hidden "false" (the framework's checkbox); the first value wins.
        Assert.Equal(22, CardNames(await PageAsync(token, "?inDate=true&inDate=false")).Count);
    }

    [Fact]
    public async Task The_chips_are_Tat_ca_and_the_five_categories_and_one_chip_filters_the_items()
    {
        var token = await SavedHamperAsync(Delivery);

        var all = await PageAsync(token);
        var chips = Regex.Matches(all, @"<button type=""submit"" class=""btn btn--outline btn--chip"" name=""category"" value=""([^""]*)"" aria-pressed=""(true|false)"">([^<]*)</button>")
            .Select(m => (Id: m.Groups[1].Value, Pressed: m.Groups[2].Value, Label: m.Groups[3].Value)).ToList();
        Assert.Equal(["Tất cả", "Bánh", "Hạt và trái cây sấy", "Mật ong và đặc sản", "Mứt và kẹo", "Trà và cà phê"], chips.Select(c => c.Label));
        Assert.Equal("true", chips[0].Pressed);

        // Bánh in date for 15/01/2027: Bánh quy bơ (10/01) is out; the other four are in.
        var banh = await PageAsync(token, "?category=banh");
        Assert.Equal(new[] { "Bánh đậu xanh", "Bánh gừng", "Bánh pía mini", "Bánh quy yến mạch" }.Order(StringComparer.Ordinal), CardNames(banh).Order(StringComparer.Ordinal));
        Assert.Contains("Hiện 4 món. 1 món hết hạn trước ngày giao đang được ẩn.", banh);
        Assert.Contains("name=\"category\" value=\"banh\" aria-pressed=\"true\"", banh);
        Assert.Matches(@"<a href=""/builder\?category=banh&inDate=false#chon-mon"">Hiện cả 1 món</a>", banh);   // decoded: &amp; became &

        // An unknown category (a typo, an old link) shows the items of every category.
        Assert.Equal(22, CardNames(await PageAsync(token, "?category=khong-co")).Count);
    }

    [Fact]
    public async Task A_sold_out_item_shows_Het_hang_instead_of_its_expiry()
    {
        var token = await SavedHamperAsync(Delivery);
        var stockBefore = await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "tra-sen").Select(p => p.Stock).SingleAsync());
        await SetStockAsync("tra-sen", 0);
        try
        {
            var html = await PageAsync(token);
            var card = Regex.Match(html, @"<li class=""card item-card item-card--sold-out"">[\s\S]*?</li>").Value;

            Assert.Contains("Trà sen", card);
            Assert.Contains("<span class=\"badge badge--muted\">Hết hàng</span>", card);
            Assert.DoesNotContain("HSD", card);
            Assert.Single(Regex.Matches(html, "item-card--sold-out"));
        }
        finally
        {
            await SetStockAsync("tra-sen", stockBefore);
        }
    }

    [Fact]
    public async Task A_wrong_budget_keeps_the_items_of_the_date_saved_before()
    {
        var token = await SavedHamperAsync(Delivery);
        var client = Client();

        var get = await client.SendAsync(WithCookie("/builder", token));
        var page = WebUtility.HtmlDecode(await get.Content.ReadAsStringAsync());
        var antiForgeryCookie = get.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Antiforgery")).Split(';')[0];
        var post = new HttpRequestMessage(HttpMethod.Post, "/builder")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["budget"] = "abc",
                ["__RequestVerificationToken"] = Regex.Match(page, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value,
            }),
        };
        post.Headers.Add("Cookie", $"{HamperService.CookieName}={token}; {antiForgeryCookie}");

        var html = WebUtility.HtmlDecode(await (await client.SendAsync(post)).Content.ReadAsStringAsync());

        Assert.Contains("Nhập ngân sách bằng số", html);
        Assert.Equal(22, CardNames(html).Count);
    }

    private static HttpRequestMessage WithCookie(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"{HamperService.CookieName}={token}");
        return request;
    }

    private Task SetStockAsync(string slug, int stock) =>
        fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == slug).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, stock)));
}

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// "Thêm", − and + on /builder and "Hộp của bạn" (F20, F21, F23). Budget 500.000đ, delivery 15/01/2027. The amounts are
// written out on purpose: a test that worked them out with Pricing would agree with a mistake in it.
// Seed prices: Trà sen 180.000, Hạt điều 150.000, Mứt gừng 90.000, Kẹo dừa 55.000 (stock 5), Muối tôm 50.000.
[Collection(DatabaseCollection.Name)]
public class BuilderBoxTests(PeekboxFixture fixture)
{
    private static readonly DateOnly Delivery = new(2027, 1, 15);

    // A visitor who already saved a budget and a date, with the anti-forgery cookie and token of the page.
    private sealed class Visitor(HttpClient client, string hamperToken)
    {
        private string _antiForgeryCookie = "";
        private string _formToken = "";

        public string HamperToken => hamperToken;

        public async Task<string> PageAsync(string query = "")
        {
            var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, "/builder" + query));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var raw = await response.Content.ReadAsStringAsync();
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _antiForgeryCookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal))?.Split(';')[0] ?? _antiForgeryCookie;
            }

            _formToken = Regex.Match(raw, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
            return WebUtility.HtmlDecode(raw);
        }

        // Presses a button: posts the item and the amount, as the card's form does.
        public async Task<HttpResponseMessage> SetAsync(string slug, int quantity, string query = "", bool withToken = true)
        {
            if (_formToken.Length == 0)
            {
                await PageAsync();
            }

            var fields = new Dictionary<string, string> { ["slug"] = slug, ["quantity"] = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            if (withToken)
            {
                fields["__RequestVerificationToken"] = _formToken;
            }

            return await SendAsync(new HttpRequestMessage(HttpMethod.Post, "/builder/quantity" + query) { Content = new FormUrlEncodedContent(fields) });
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            request.Headers.Add("Cookie", string.Join("; ", new[] { $"{HamperService.CookieName}={hamperToken}", _antiForgeryCookie }.Where(c => c.Length > 0)));
            return client.SendAsync(request);
        }
    }

    private async Task<Visitor> VisitorAsync(long budget = 500_000, DateOnly? delivery = null)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(db =>
        {
            db.Hampers.Add(new Hamper { Token = token, BudgetVnd = budget, DeliveryDate = delivery ?? Delivery, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });
        return new Visitor(fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false }), token);
    }

    private Task<Dictionary<string, int>> LinesAsync(Visitor visitor) =>
        fixture.WithDbAsync(db => db.HamperItems.Where(l => l.Hamper!.Token == visitor.HamperToken)
            .Select(l => new { l.Product!.Slug, l.Quantity }).ToDictionaryAsync(l => l.Slug, l => l.Quantity));

    private static string Card(string html, string slug) => Regex.Match(html, $@"<li class=""card item-card[^""]*"" id=""mon-{slug}"">[\s\S]*?</li>").Value;

    private static string Panel(string html) => Regex.Match(html, @"<aside class=""card box-panel""[\s\S]*?</aside>").Value;

    private static string Sum(string html, string label) =>
        Regex.Match(Panel(html), $@"<dt>{Regex.Escape(label)}</dt><dd>([^<]*)</dd>").Groups[1].Value;

    [Fact]
    public async Task Them_puts_one_in_the_box_and_the_card_shows_the_pill_and_the_panel_the_line()
    {
        var visitor = await VisitorAsync();

        var response = await visitor.SetAsync("tra-sen", 1);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/builder#mon-tra-sen", response.Headers.Location?.OriginalString);
        Assert.Equal(new Dictionary<string, int> { ["tra-sen"] = 1 }, await LinesAsync(visitor));

        var html = await visitor.PageAsync();
        var card = Card(html, "tra-sen");
        Assert.Contains("<span class=\"qty__count\">1</span>", card);
        Assert.Contains("aria-label=\"Bớt một Trà sen\"", card);
        Assert.DoesNotContain(">Thêm</button>", card);
        Assert.Contains("<span class=\"box-line__name\">Trà sen</span><span>180.000đ</span>", Panel(html));
        Assert.Equal("320.000đ", Sum(html, "Còn lại"));
        Assert.Equal("180.000đ", Sum(html, "Tổng cộng (đã gồm thuế)"));
        Assert.Contains("Cả 1 món đều còn hạn đến sau ngày giao 15/01/2027 cộng 7 ngày dự phòng.", html);
        Assert.DoesNotContain("id=\"packaging-lock\"", html);   // step 3 is no longer locked
        Assert.Contains("1 món · Còn lại <strong>320.000đ</strong>", html);   // the phone bar
    }

    [Fact]
    public async Task An_empty_box_says_so_and_keeps_packaging_locked_and_continue_off()
    {
        var html = await (await VisitorAsync()).PageAsync();

        Assert.Contains("Chưa có món nào trong hộp.", html);
        Assert.Contains("<p class=\"lock-note\" id=\"packaging-lock\">", html);
        Assert.Equal("500.000đ", Sum(html, "Còn lại"));
        Assert.Equal("0đ", Sum(html, "Tổng cộng (đã gồm thuế)"));
        Assert.Contains("<button type=\"button\" class=\"btn btn--primary box-panel__next\" disabled aria-describedby=\"next-soon\">Tiếp tục: xem lại hộp quà</button>", html);
        Assert.Contains(">Thêm</button>", Card(html, "tra-sen"));
    }

    [Fact]
    public async Task Plus_and_minus_change_the_amount_and_minus_at_one_takes_it_out_and_brings_Them_back()
    {
        var visitor = await VisitorAsync();
        await visitor.SetAsync("tra-sen", 1);
        await visitor.SetAsync("tra-sen", 2);

        var two = await visitor.PageAsync();
        Assert.Contains("<span class=\"qty__count\">2</span>", Card(two, "tra-sen"));
        Assert.Contains("<span class=\"box-line__name\">Trà sen</span><span>360.000đ</span>", Panel(two));   // the line, not the unit price
        Assert.Contains("name=\"quantity\" value=\"1\" aria-label=\"Bớt một Trà sen\"", Card(two, "tra-sen"));
        Assert.Contains("name=\"quantity\" value=\"3\" aria-label=\"Thêm một Trà sen\"", Card(two, "tra-sen"));

        await visitor.SetAsync("tra-sen", 1);
        await visitor.SetAsync("tra-sen", 0);

        Assert.Empty(await LinesAsync(visitor));
        var none = await visitor.PageAsync();
        Assert.Contains(">Thêm</button>", Card(none, "tra-sen"));
        Assert.Contains("Chưa có món nào trong hộp.", none);
    }

    [Fact]
    public async Task The_budget_is_a_hard_cap_and_costing_exactly_what_is_left_is_fine()
    {
        var visitor = await VisitorAsync();
        await visitor.SetAsync("tra-sen", 2);   // 360.000, 140.000 left
        await visitor.SetAsync("mut-gung", 1);  // 90.000, 50.000 left

        var html = await visitor.PageAsync();
        Assert.Contains("disabled>Vượt 100.000đ</button>", Card(html, "hat-dieu-rang-muoi"));   // 150.000 - 50.000
        Assert.Contains("disabled>Vượt 5.000đ</button>", Card(html, "keo-dua"));                // 55.000 - 50.000
        Assert.Contains("value=\"3\" aria-label=\"Thêm một Trà sen\" data-qty-step=\"plus\" disabled", Card(html, "tra-sen"));
        Assert.Contains(">Thêm</button>", Card(html, "muoi-tom-tay-ninh"));                     // 50.000: exactly what is left

        // A button the page showed as off changes nothing when sent anyway.
        await visitor.SetAsync("hat-dieu-rang-muoi", 1);
        await visitor.SetAsync("tra-sen", 3);
        Assert.Equal(new Dictionary<string, int> { ["tra-sen"] = 2, ["mut-gung"] = 1 }, await LinesAsync(visitor));

        await visitor.SetAsync("muoi-tom-tay-ninh", 1);
        Assert.Equal("0đ", Sum(await visitor.PageAsync(), "Còn lại"));
    }

    [Fact]
    public async Task The_stock_is_a_cap_too()
    {
        var visitor = await VisitorAsync();
        await visitor.SetAsync("keo-dua", 5);   // the whole stock; a jump to the amount is checked on the last one added

        var html = await visitor.PageAsync();
        Assert.Contains("value=\"6\" aria-label=\"Thêm một Kẹo dừa\" data-qty-step=\"plus\" disabled", Card(html, "keo-dua"));

        await visitor.SetAsync("keo-dua", 6);
        Assert.Equal(5, (await LinesAsync(visitor))["keo-dua"]);
    }

    [Fact]
    public async Task An_item_out_of_date_for_the_delivery_date_cannot_go_in()
    {
        var visitor = await VisitorAsync();

        await visitor.SetAsync("banh-quy-bo", 1);   // expires 10/01/2027, before 15/01 + 7 days

        Assert.Empty(await LinesAsync(visitor));
        Assert.Contains("disabled>Không chọn được cho ngày giao này</button>", Card(await visitor.PageAsync("?inDate=false"), "banh-quy-bo"));
    }

    [Fact]
    public async Task A_line_that_went_out_of_date_after_the_date_changed_is_flagged_in_the_panel()
    {
        var visitor = await VisitorAsync();
        await visitor.SetAsync("mut-gung", 1);   // expires 31/03/2027
        await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.DeliveryDate, new DateOnly(2027, 3, 28))));

        var panel = Panel(await visitor.PageAsync());

        Assert.Contains("<span class=\"badge badge--warn\">HSD 31/03/2027, trước ngày giao</span>", panel);
        Assert.Contains("1 món hết hạn trước ngày giao. Bỏ món đó ra hoặc đổi ngày giao.", panel);
    }

    [Fact]
    public async Task After_the_budget_is_lowered_the_box_shows_how_far_over_and_items_can_still_come_out()
    {
        var visitor = await VisitorAsync();
        await visitor.SetAsync("tra-sen", 2);   // 360.000
        await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.BudgetVnd, 300_000L)));

        var html = await visitor.PageAsync();
        Assert.Equal("60.000đ", Sum(html, "Vượt ngân sách"));
        Assert.Contains("2 món · Vượt <strong>60.000đ</strong>", html);

        await visitor.SetAsync("tra-sen", 1);
        Assert.Equal(1, (await LinesAsync(visitor))["tra-sen"]);
    }

    [Fact]
    public async Task Made_up_requests_change_nothing_or_are_refused()
    {
        var visitor = await VisitorAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.SetAsync("khong-co", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.SetAsync("tra-sen", 1, withToken: false)).StatusCode);   // anti-forgery
        await visitor.SetAsync("tra-sen", 9);   // 9 x 180.000 is over the budget
        Assert.Empty(await LinesAsync(visitor));

        await visitor.SetAsync("tra-sen", 1);
        await visitor.SetAsync("tra-sen", -5);   // below 0 takes it out, nothing worse
        Assert.Empty(await LinesAsync(visitor));
    }

    [Fact]
    public async Task Without_a_hamper_or_with_the_items_locked_nothing_is_saved()
    {
        var locked = await VisitorAsync();
        await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == locked.HamperToken)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.DeliveryDate, (DateOnly?)null)));

        var response = await locked.SetAsync("tra-sen", 1);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(await LinesAsync(locked));

        var stranger = new Visitor(fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false }), "not-a-token");
        Assert.Equal(HttpStatusCode.Redirect, (await stranger.SetAsync("tra-sen", 1)).StatusCode);
    }

    [Fact]
    public async Task The_redirect_keeps_the_list_filters_and_the_forms_post_with_them()
    {
        var visitor = await VisitorAsync();
        var html = await visitor.PageAsync("?category=banh&q=gung&sort=price-asc&inDate=false");

        Assert.Contains("action=\"/builder/quantity?category=banh&q=gung&sort=price-asc&inDate=false\"", html);

        var response = await visitor.SetAsync("banh-gung", 1, "?category=banh&q=gung&sort=price-asc&inDate=false");
        Assert.Equal("/builder?category=banh&q=gung&sort=price-asc&inDate=false#mon-banh-gung", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Deleting_a_hamper_deletes_its_lines_and_an_item_in_a_hamper_cannot_be_deleted()
    {
        var visitor = await VisitorAsync();
        await visitor.SetAsync("tra-sen", 1);

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.WithDbAsync(async db =>
        {
            db.Products.Remove(await db.Products.SingleAsync(p => p.Slug == "tra-sen"));
            return await db.SaveChangesAsync();
        }));

        await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken).ExecuteDeleteAsync());
        Assert.Empty(await LinesAsync(visitor));
    }

    [Fact]
    public async Task The_page_is_well_formed_with_items_in_the_box_and_over_budget()
    {
        var visitor = await VisitorAsync();
        await visitor.SetAsync("tra-sen", 2);
        await visitor.SetAsync("mut-gung", 1);
        BuilderItemsTests.AssertWellFormed("items in the box", await RawAsync(visitor));

        await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.BudgetVnd, 100_000L)));
        BuilderItemsTests.AssertWellFormed("over budget", await RawAsync(visitor));
    }

    // AssertWellFormed reads ids, labels and file paths, which decoding does not change.
    private static Task<string> RawAsync(Visitor visitor) => visitor.PageAsync();
}

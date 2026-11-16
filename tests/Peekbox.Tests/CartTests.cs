using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// The cart (F37, F39, F40), "Thêm vào giỏ hàng" (F36) and the header's count. The cart is the visitor's hamper once it
// holds an item (decision 2026-11-16). Budget 500.000đ, delivery 15/01/2027. Amounts are written out on purpose.
// Seed: Trà sen 180.000, Hạt điều rang muối 150.000, Mứt gừng 90.000 (expires 31/03/2027); hard box with bow 40.000.
[Collection(DatabaseCollection.Name)]
public class CartTests(PeekboxFixture fixture)
{
    private static readonly DateOnly Delivery = new(2027, 1, 15);

    private static DateOnly TodayInVietnam => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private sealed class Visitor(HttpClient client, string? hamperToken)
    {
        private string _antiForgeryCookie = "";
        private string _formToken = "";

        public string? HamperToken => hamperToken;

        public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

        // Reads a page and its anti-forgery token; the text comes back decoded, with runs of spaces made one.
        public async Task<string> PageAsync(string path)
        {
            var response = await GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var raw = await response.Content.ReadAsStringAsync();
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _antiForgeryCookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal))?.Split(';')[0] ?? _antiForgeryCookie;
            }

            var token = Regex.Match(raw, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
            _formToken = token.Length > 0 ? token : _formToken;
            return WebUtility.HtmlDecode(Regex.Replace(raw, @"\s+", " "));
        }

        public async Task<HttpResponseMessage> PostAsync(string path, Dictionary<string, string>? fields = null, bool withToken = true)
        {
            if (_formToken.Length == 0)
            {
                await PageAsync("/builder");
            }

            fields ??= [];
            if (withToken)
            {
                fields["__RequestVerificationToken"] = _formToken;
            }

            return await SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) });
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            var cookies = new[] { hamperToken is null ? "" : $"{HamperService.CookieName}={hamperToken}", _antiForgeryCookie }.Where(c => c.Length > 0).ToList();
            if (cookies.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", cookies));
            }

            return client.SendAsync(request);
        }
    }

    private HttpClient Client() => fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private async Task<Visitor> VisitorAsync(DateOnly? delivery, params (string Slug, int Quantity)[] lines)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(db =>
        {
            db.Hampers.Add(new Hamper { Token = token, BudgetVnd = 500_000, DeliveryDate = delivery, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });
        var visitor = new Visitor(Client(), token);
        foreach (var (slug, quantity) in lines)
        {
            await visitor.PostAsync("/builder/quantity", new() { ["slug"] = slug, ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture) });
        }

        return visitor;
    }

    private Task SetDeliveryAsync(Visitor visitor, DateOnly date) =>
        fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken).ExecuteUpdateAsync(s => s.SetProperty(h => h.DeliveryDate, date)));

    private static string CartLink(string html) => Regex.Match(html, @"<a class=""cart-link""[\s\S]*?</a>").Value;

    private static string Summary(string html) => Regex.Match(html, @"<aside class=""card cart-summary""[\s\S]*?</aside>").Value;

    [Fact]
    public async Task Without_a_hamper_the_cart_is_empty_and_the_header_says_0()
    {
        var html = await new Visitor(Client(), null).PageAsync("/cart");

        Assert.Contains("<title>Giỏ hàng · Peekbox</title>", html);
        Assert.Contains("<h2 class=\"cart-empty__title\" id=\"empty-title\">Giỏ hàng đang trống</h2>", html);
        Assert.Contains("Hãy đặt ngân sách trước, rồi chọn từng món. Peekbox sẽ cho bạn thấy còn lại bao nhiêu sau mỗi món.", html);
        Assert.Contains("<a class=\"btn btn--primary cart-empty__go\" href=\"/builder\">Tạo hộp quà</a>", html);
        Assert.DoesNotContain("Xem hộp gợi ý", html);   // starter hampers (F15) come later
        Assert.DoesNotContain("Mã giảm giá", html);     // coupons (F38) come later
        var link = CartLink(html);
        Assert.Contains("aria-label=\"Giỏ hàng, trống\" aria-current=\"page\"", link);
        Assert.Contains("<span class=\"cart-link__text\">Giỏ hàng (0)</span>", link);
    }

    [Fact]
    public async Task A_hamper_without_items_is_an_empty_cart()
    {
        var html = await (await VisitorAsync(Delivery)).PageAsync("/cart");

        Assert.Contains("Giỏ hàng đang trống", html);
        Assert.Contains("Giỏ hàng (0)", CartLink(html));
    }

    [Fact]
    public async Task Once_the_hamper_holds_an_item_every_header_counts_it()
    {
        var visitor = await VisitorAsync(Delivery, ("tra-sen", 1));

        foreach (var path in new[] { "/", "/builder", "/items/tra-sen" })
        {
            var link = CartLink(await visitor.PageAsync(path));
            Assert.Contains("aria-label=\"Giỏ hàng, 1 hộp\">", link);   // not the current page here
            Assert.Contains("<span class=\"cart-link__text\">Giỏ hàng (1)</span>", link);
        }
    }

    [Fact]
    public async Task The_cart_shows_the_box_and_its_totals_and_checkout_waits_for_the_next_slice()
    {
        var visitor = await VisitorAsync(Delivery, ("tra-sen", 1), ("hat-dieu-rang-muoi", 1), ("mut-gung", 1));
        await visitor.PostAsync("/builder/packaging", new() { ["packaging"] = "hard-bow" });

        var html = await visitor.PageAsync("/cart");

        Assert.Contains("aria-label=\"Giỏ hàng, 1 hộp\" aria-current=\"page\"", CartLink(html));
        Assert.Contains("<p class=\"cart-box__meta\">Giao ngày 15/01/2027 · Hộp cứng có nơ · Ngân sách 500.000đ</p>", html);
        Assert.Contains("<a class=\"btn btn--outline\" href=\"/builder#chon-mon\">Sửa hộp</a>", html);
        Assert.Contains("<button type=\"button\" class=\"btn btn--outline cart-box__clear\" popovertarget=\"xoa-hop\">Xóa hộp</button>", html);
        Assert.Contains("<p class=\"review-line__name\">Trà sen</p>", html);
        Assert.Contains("<p class=\"review-line__name\">Mứt gừng</p>", html);
        Assert.Contains("<p class=\"cart-box__packaging\"><span>Hộp cứng có nơ</span><strong>40.000đ</strong></p>", html);
        Assert.DoesNotContain("Bỏ món này", html);

        var summary = Summary(html);
        Assert.Contains("<div><dt>Tiền hàng (3 món)</dt><dd>420.000đ</dd></div>", summary);
        Assert.Contains("<div><dt>Đóng gói</dt><dd>40.000đ</dd></div>", summary);
        Assert.Contains("<div class=\"cart-sums__later\"><dt>Phí giao hàng</dt><dd>Tính ở bước sau</dd></div>", summary);
        Assert.Contains("<p class=\"cart-total\"><span>Tạm tính (đã gồm thuế)</span><strong>460.000đ</strong></p>", summary);
        Assert.Contains("<button type=\"button\" class=\"btn btn--primary cart-checkout\" disabled aria-describedby=\"checkout-reason\">Tiếp tục thanh toán</button>", summary);
        Assert.Contains("<p class=\"cart-reason\" id=\"checkout-reason\">Thanh toán sẽ có trong bản sau.</p>", summary);
        Assert.Contains("<div class=\"cart-bar\" role=\"region\" aria-label=\"Tạm tính\">", html);
        Assert.Contains("<div class=\"cart-confirm\" id=\"xoa-hop\" popover role=\"dialog\" aria-labelledby=\"confirm-title\" aria-describedby=\"confirm-text\">", html);
    }

    [Fact]
    public async Task Them_vao_gio_hang_checks_the_box_again_and_opens_the_cart()
    {
        var ready = await VisitorAsync(Delivery, ("tra-sen", 1));
        Assert.Equal("/cart", (await ready.PostAsync("/cart/add")).Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await ready.PostAsync("/cart/add", withToken: false)).StatusCode);

        var empty = await VisitorAsync(Delivery);
        Assert.Equal("/builder", (await empty.PostAsync("/cart/add")).Headers.Location?.OriginalString);

        var stale = await VisitorAsync(Delivery, ("tra-sen", 1), ("mut-gung", 1));
        await SetDeliveryAsync(stale, new DateOnly(2027, 3, 28));   // Mứt gừng expires 31/03/2027
        Assert.Equal("/builder/review", (await stale.PostAsync("/cart/add")).Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Xoa_hop_takes_the_items_and_packaging_out_and_keeps_the_budget_and_date()
    {
        var visitor = await VisitorAsync(Delivery, ("tra-sen", 2));
        await visitor.PostAsync("/builder/packaging", new() { ["packaging"] = "rattan" });
        await visitor.PageAsync("/cart");

        var response = await visitor.PostAsync("/cart/clear");

        Assert.Equal("/cart", response.Headers.Location?.OriginalString);
        var hamper = await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken)
            .Select(h => new { h.BudgetVnd, h.DeliveryDate, h.PackagingId, Lines = h.Items.Count }).SingleAsync());
        Assert.Equal((500_000L, (DateOnly?)Delivery, (string?)null, 0), (hamper.BudgetVnd, hamper.DeliveryDate, hamper.PackagingId, hamper.Lines));
        var html = await visitor.PageAsync("/cart");
        Assert.Contains("Giỏ hàng đang trống", html);
        Assert.Contains("Giỏ hàng (0)", CartLink(html));
    }

    [Fact]
    public async Task An_item_out_of_date_is_blocked_in_the_cart_too_and_can_be_removed_there()
    {
        var visitor = await VisitorAsync(Delivery, ("tra-sen", 1), ("mut-gung", 1));
        await SetDeliveryAsync(visitor, new DateOnly(2027, 3, 28));

        var html = await visitor.PageAsync("/cart");

        Assert.Contains("Chưa thể đặt hàng: 1 món hết hạn trước ngày giao", html);
        Assert.Contains("action=\"/builder/quantity?from=cart\"", html);
        Assert.Contains("<p class=\"cart-reason cart-reason--block\" id=\"checkout-reason\">Bỏ món hết hạn hoặc đổi ngày giao để tiếp tục.</p>", Summary(html));

        var response = await visitor.PostAsync("/builder/quantity?from=cart", new() { ["slug"] = "mut-gung", ["quantity"] = "0" });

        Assert.Equal("/cart", response.Headers.Location?.OriginalString);
        var after = await visitor.PageAsync("/cart");
        Assert.DoesNotContain("review-block", after);
        Assert.Contains("<p class=\"cart-reason\" id=\"checkout-reason\">Thanh toán sẽ có trong bản sau.</p>", Summary(after));
    }

    [Fact]
    public async Task A_delivery_date_that_has_passed_is_the_reason_checkout_is_off()
    {
        var visitor = await VisitorAsync(Delivery, ("tra-sen", 1));
        await SetDeliveryAsync(visitor, TodayInVietnam);

        var html = await visitor.PageAsync("/cart");

        Assert.Contains("<p class=\"cart-reason cart-reason--block\" id=\"checkout-reason\">Ngày giao đã qua. Bấm Sửa hộp để chọn ngày giao mới.</p>", Summary(html));
        Assert.Equal("/builder", (await visitor.PostAsync("/cart/add")).Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Every_state_of_the_cart_is_well_formed()
    {
        BuilderItemsTests.AssertWellFormed("empty", await (await new Visitor(Client(), null).GetAsync("/cart")).Content.ReadAsStringAsync());
        var visitor = await VisitorAsync(Delivery, ("tra-sen", 2), ("mut-gung", 1));
        BuilderItemsTests.AssertWellFormed("full", await (await visitor.GetAsync("/cart")).Content.ReadAsStringAsync());
        await SetDeliveryAsync(visitor, new DateOnly(2027, 3, 28));
        BuilderItemsTests.AssertWellFormed("blocked", await (await visitor.GetAsync("/cart")).Content.ReadAsStringAsync());
    }
}

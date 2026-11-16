using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// Step 3 (packaging, F33), "Tiếp tục" (F24) and step 4, the review page (F34 to F36). Budget 500.000đ, delivery
// 15/01/2027. Amounts are written out on purpose: a test that worked them out with Pricing would agree with a mistake in
// it. Seed: Trà sen 180.000 (expires 31/12/2027), Hạt điều rang muối 150.000, Mứt gừng 90.000 (expires 31/03/2027).
// Packaging: kraft 0, hard box with bow 40.000, rattan basket 70.000.
[Collection(DatabaseCollection.Name)]
public class PackagingReviewTests(PeekboxFixture fixture)
{
    private static readonly DateOnly Delivery = new(2027, 1, 15);

    private sealed class Visitor(HttpClient client, string? hamperToken)
    {
        private string _antiForgeryCookie = "";
        private string _formToken = "";

        public string? HamperToken => hamperToken;

        public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

        // Reads a page (and its anti-forgery token); the text comes back decoded, with runs of spaces made one.
        public async Task<string> PageAsync(string path)
        {
            var response = await GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var raw = await response.Content.ReadAsStringAsync();
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _antiForgeryCookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal))?.Split(';')[0] ?? _antiForgeryCookie;
            }

            _formToken = Regex.Match(raw, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
            return raw;
        }

        public async Task<HttpResponseMessage> PostAsync(string path, Dictionary<string, string> fields, bool withToken = true)
        {
            if (_formToken.Length == 0)
            {
                await PageAsync("/builder");
            }

            if (withToken)
            {
                fields["__RequestVerificationToken"] = _formToken;
            }

            return await SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) });
        }

        public Task<HttpResponseMessage> AddAsync(string slug, int quantity) =>
            PostAsync("/builder/quantity", new() { ["slug"] = slug, ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture) });

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

    private async Task<Visitor> VisitorAsync(params (string Slug, int Quantity)[] lines)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(db =>
        {
            db.Hampers.Add(new Hamper { Token = token, BudgetVnd = 500_000, DeliveryDate = Delivery, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });
        var visitor = new Visitor(Client(), token);
        foreach (var (slug, quantity) in lines)
        {
            await visitor.AddAsync(slug, quantity);
        }

        return visitor;
    }

    private Task<string?> PackagingOfAsync(Visitor visitor) =>
        fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken).Select(h => h.PackagingId).SingleAsync());

    private static string Text(string html) => WebUtility.HtmlDecode(Regex.Replace(html, @"\s+", " "));

    private static string Panel(string html) => Regex.Match(html, @"<aside class=""card box-panel""[\s\S]*?</aside>").Value;

    private static string Summary(string html) => Regex.Match(html, @"<aside class=""card review-summary""[\s\S]*?</aside>").Value;

    private static string Line(string html, string name) =>
        Regex.Match(html, $@"<li class=""review-line[^""]*""> <img[^>]*> <p class=""review-line__name"">{Regex.Escape(name)}</p>[\s\S]*?</li>").Value;

    [Fact]
    public async Task With_an_item_in_the_box_packaging_opens_with_kraft_chosen_and_continue_is_on()
    {
        var html = Text(await (await VisitorAsync(("tra-sen", 1))).PageAsync("/builder"));

        Assert.Contains("<li class=\"builder-step\" aria-current=\"step\"><span class=\"builder-step__num\">3</span><span>Đóng gói</span></li>", html);
        Assert.DoesNotContain("id=\"packaging-lock\"", html);
        Assert.Contains("<form id=\"dong-goi-form\" method=\"post\" action=\"/builder/packaging\">", html);
        Assert.Contains("value=\"kraft\" checked=\"checked\">", html);
        Assert.Contains("<span class=\"packaging-option__name\">Hộp cứng có nơ</span>", html);
        Assert.Contains("<span class=\"packaging-option__fee\">Miễn phí</span>", html);
        Assert.Contains("<span class=\"packaging-option__fee\">+40.000đ</span>", html);
        Assert.Contains("<span class=\"packaging-option__fee\">+70.000đ</span>", html);
        Assert.Contains("<div class=\"box-sums__row\"><dt>Đóng gói: Hộp giấy kraft</dt><dd>0đ</dd></div>", Panel(html));
        Assert.Contains("<button type=\"submit\" class=\"btn btn--primary box-panel__next\" form=\"dong-goi-form\" name=\"next\" value=\"review\">Tiếp tục: xem lại hộp quà</button>", html);
        Assert.Contains("<button type=\"submit\" class=\"btn btn--primary box-bar__next\" form=\"dong-goi-form\" name=\"next\" value=\"review\">Xem lại hộp quà</button>", html);
    }

    [Fact]
    public async Task Choosing_a_packaging_saves_it_and_adds_its_fee_on_top_of_the_budget()
    {
        var visitor = await VisitorAsync(("tra-sen", 1));

        var response = await visitor.PostAsync("/builder/packaging", new() { ["packaging"] = "hard-bow" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/builder#dong-goi", response.Headers.Location?.OriginalString);
        Assert.Equal("hard-bow", await PackagingOfAsync(visitor));
        var html = Text(await visitor.PageAsync("/builder"));
        Assert.Contains("value=\"hard-bow\" checked=\"checked\">", html);
        var panel = Panel(html);
        Assert.Contains("<dt>Đóng gói: Hộp cứng có nơ</dt><dd>40.000đ</dd>", panel);
        Assert.Contains("<dt>Còn lại</dt><dd>320.000đ</dd>", panel);                     // the budget covers the goods only
        Assert.Contains("<dt>Tổng cộng (đã gồm thuế)</dt><dd>220.000đ</dd>", panel);     // 180.000 + 40.000
    }

    [Fact]
    public async Task Continue_saves_the_packaging_on_screen_and_opens_the_review_page()
    {
        var visitor = await VisitorAsync(("tra-sen", 1));

        var response = await visitor.PostAsync("/builder/packaging", new() { ["packaging"] = "rattan", ["next"] = "review" });

        Assert.Equal("/builder/review", response.Headers.Location?.OriginalString);
        Assert.Equal("rattan", await PackagingOfAsync(visitor));
    }

    [Fact]
    public async Task An_unknown_packaging_an_empty_box_or_a_missing_token_saves_nothing()
    {
        var visitor = await VisitorAsync(("tra-sen", 1));
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsync("/builder/packaging", new() { ["packaging"] = "vali" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync("/builder/packaging", new() { ["packaging"] = "rattan" }, withToken: false)).StatusCode);
        Assert.Null(await PackagingOfAsync(visitor));

        var empty = await VisitorAsync();
        var response = await empty.PostAsync("/builder/packaging", new() { ["packaging"] = "rattan" });
        Assert.Equal("/builder", response.Headers.Location?.OriginalString);
        Assert.Null(await PackagingOfAsync(empty));
    }

    [Fact]
    public async Task There_is_no_review_without_a_hamper_or_with_an_empty_box()
    {
        Assert.Equal("/builder", (await new Visitor(Client(), null).GetAsync("/builder/review")).Headers.Location?.OriginalString);
        Assert.Equal("/builder", (await (await VisitorAsync()).GetAsync("/builder/review")).Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task The_review_page_shows_the_hamper_read_only_with_the_summary()
    {
        var visitor = await VisitorAsync(("tra-sen", 1), ("hat-dieu-rang-muoi", 1), ("mut-gung", 1));

        var html = Text(await visitor.PageAsync("/builder/review"));

        Assert.Contains("<title>Xem lại hộp quà · Peekbox</title>", html);
        Assert.Contains("<li class=\"builder-step builder-step--done\"><span class=\"builder-step__num\">3</span><span>Đóng gói</span><span class=\"visually-hidden\">, đã xong</span></li>", html);
        Assert.Contains("<li class=\"builder-step\" aria-current=\"step\"><span class=\"builder-step__num\">4</span><span>Xem lại</span></li>", html);
        Assert.Contains("<div><dt>Ngân sách</dt><dd>500.000đ</dd></div> <div><dt>Ngày giao hàng</dt><dd>15/01/2027</dd></div>", html);
        Assert.Contains("<a class=\"review-card__edit\" href=\"/builder\" aria-label=\"Sửa ngân sách và ngày giao\">Sửa</a>", html);
        Assert.Contains("<a class=\"review-card__edit\" href=\"/builder#dong-goi\" aria-label=\"Đổi kiểu đóng gói\">Đổi</a>", html);
        Assert.Contains("<p class=\"review-packaging__name\">Hộp giấy kraft</p>", html);
        Assert.Contains("<p class=\"review-packaging__fee\">Miễn phí</p>", html);
        Assert.Contains("<strong>180.000đ</strong>", Line(html, "Trà sen"));
        Assert.Contains("Thương hiệu A · 100 g · Xuất xứ Hà Nội", Line(html, "Trà sen"));
        Assert.Contains("<span class=\"badge\">HSD 12/2027 · đủ hạn</span>", Line(html, "Trà sen"));
        Assert.DoesNotContain("Bỏ món này", html);
        Assert.DoesNotContain("review-block", html);
        Assert.Contains("<a class=\"review-card__back\" href=\"/builder#chon-mon\">Quay lại chỉnh sửa món</a>", html);

        var summary = Summary(html);
        Assert.Contains("<dt>3 món</dt><dd>420.000đ</dd>", summary);
        Assert.Contains("<dt>Hộp giấy kraft</dt><dd>0đ</dd>", summary);
        Assert.Contains("<dt>Còn lại</dt><dd>80.000đ</dd>", summary);
        Assert.Contains("<dt>Tổng cộng (đã gồm thuế)</dt><dd>420.000đ</dd>", summary);
        Assert.Contains("Cả 3 món đều còn hạn đến sau ngày giao 15/01/2027 cộng 7 ngày dự phòng.", summary);
        Assert.Contains("<form class=\"review-summary__add\" method=\"post\" action=\"/cart/add\">", summary);
        Assert.Contains("<button type=\"submit\" class=\"btn btn--primary review-summary__cart\">Thêm vào giỏ hàng</button>", summary);
        Assert.DoesNotContain("disabled", summary);

        // The phone sheet: a popover the bar opens, with the same summary.
        Assert.Contains("popovertarget=\"tong-ket\"", html);
        Assert.Contains("<div class=\"review-sheet\" id=\"tong-ket\" popover role=\"dialog\" aria-labelledby=\"sheet-title\">", html);
        Assert.Contains("3 món · Còn lại <strong>80.000đ</strong>", html);
    }

    [Fact]
    public async Task A_line_of_two_shows_the_unit_price_and_the_line_total_and_the_chosen_packaging_counts()
    {
        var visitor = await VisitorAsync(("tra-sen", 2));
        await visitor.PostAsync("/builder/packaging", new() { ["packaging"] = "rattan" });

        var html = Text(await visitor.PageAsync("/builder/review"));

        Assert.Contains("<span class=\"review-line__each\">2 × 180.000đ</span> <strong>360.000đ</strong>", Line(html, "Trà sen"));
        Assert.Contains("<p class=\"review-packaging__fee\">70.000đ</p>", html);
        var summary = Summary(html);
        Assert.Contains("<dt>2 món</dt><dd>360.000đ</dd>", summary);
        Assert.Contains("<dt>Giỏ mây</dt><dd>70.000đ</dd>", summary);
        Assert.Contains("<dt>Còn lại</dt><dd>140.000đ</dd>", summary);
        Assert.Contains("<dt>Tổng cộng (đã gồm thuế)</dt><dd>430.000đ</dd>", summary);
    }

    [Fact]
    public async Task An_item_out_of_date_for_the_delivery_date_blocks_the_order_until_it_goes()
    {
        var visitor = await VisitorAsync(("tra-sen", 1), ("mut-gung", 1));
        await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.DeliveryDate, new DateOnly(2027, 3, 28))));   // Mứt gừng: 31/03/2027

        var html = Text(await visitor.PageAsync("/builder/review"));

        Assert.Contains("<h2 class=\"review-block__title\" id=\"block-title\">Chưa thể đặt hàng: 1 món hết hạn trước ngày giao</h2>", html);
        Assert.Contains("Mứt gừng hết hạn ngày 31/03/2027, còn ngày giao của bạn là 28/03/2027. Mỗi món cần còn hạn thêm 7 ngày dự phòng sau ngày giao. Hãy bỏ món này hoặc đổi ngày giao để tiếp tục.", html);
        var line = Line(html, "Mứt gừng");
        Assert.Contains("<li class=\"review-line review-line--expired\">", line);
        Assert.Contains("<span class=\"badge badge--danger\">HSD 31/03/2027, hết hạn trước ngày giao</span>", line);
        Assert.Contains("action=\"/builder/quantity?from=review\"", line);
        Assert.Contains("<button type=\"submit\" class=\"btn btn--danger\" name=\"quantity\" value=\"0\" aria-label=\"Bỏ Mứt gừng khỏi hộp\">Bỏ món này</button>", line);
        Assert.DoesNotContain("Bỏ món này", Line(html, "Trà sen"));
        var summary = Summary(html);
        Assert.Contains("<p class=\"review-summary__reason review-summary__reason--block\" id=\"cart-reason\">Bỏ món hết hạn hoặc đổi ngày giao để tiếp tục.</p>", summary);
        Assert.DoesNotContain("đều còn hạn", summary);

        var response = await visitor.PostAsync("/builder/quantity?from=review", new() { ["slug"] = "mut-gung", ["quantity"] = "0" });

        Assert.Equal("/builder/review", response.Headers.Location?.OriginalString);
        var after = Text(await visitor.PageAsync("/builder/review"));
        Assert.DoesNotContain("review-block", after);
        Assert.DoesNotContain("Mứt gừng", after);
    }

    [Fact]
    public async Task Removing_the_last_item_on_the_review_page_goes_back_to_the_builder()
    {
        var visitor = await VisitorAsync(("mut-gung", 1));
        await visitor.PageAsync("/builder/review");

        await visitor.PostAsync("/builder/quantity?from=review", new() { ["slug"] = "mut-gung", ["quantity"] = "0" });

        Assert.Equal("/builder", (await visitor.GetAsync("/builder/review")).Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Every_state_is_well_formed()
    {
        var visitor = await VisitorAsync(("tra-sen", 2), ("mut-gung", 1));
        BuilderItemsTests.AssertWellFormed("builder with packaging", await visitor.PageAsync("/builder"));
        BuilderItemsTests.AssertWellFormed("review", await visitor.PageAsync("/builder/review"));
        await fixture.WithDbAsync(db => db.Hampers.Where(h => h.Token == visitor.HamperToken)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.DeliveryDate, new DateOnly(2027, 3, 28))));
        BuilderItemsTests.AssertWellFormed("review, blocked", await visitor.PageAsync("/builder/review"));
    }
}

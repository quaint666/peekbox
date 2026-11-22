using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

using static Peekbox.Tests.TestDates;

namespace Peekbox.Tests;

// The item page /items/{slug} (F26 to F28) and the links to it from the builder's cards. Budget 500.000đ, delivery
// 15/01/2027. The texts and amounts are written out on purpose: a test that worked them out with the app's own code
// would agree with a mistake in it. Trà sen: 180.000đ, 100 g, Hà Nội, expires 31/12/2027, category Trà và cà phê.
[Collection(DatabaseCollection.Name)]
public class ItemPageTests(PeekboxFixture fixture)
{
    private static readonly DateOnly Delivery = new(2027, 1, 15);

    // A visitor with a cookie (or none), and the anti-forgery cookie and token of the last page read.
    private sealed class Visitor(HttpClient client, string? hamperToken)
    {
        private string _antiForgeryCookie = "";
        private string _formToken = "";

        public string? HamperToken => hamperToken;

        public async Task<HttpResponseMessage> GetAsync(string path) => await SendAsync(new HttpRequestMessage(HttpMethod.Get, path));

        public async Task<string> PageAsync(string slug = "tra-sen")
        {
            var response = await GetAsync("/items/" + slug);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var raw = await response.Content.ReadAsStringAsync();
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _antiForgeryCookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal))?.Split(';')[0] ?? _antiForgeryCookie;
            }

            _formToken = Regex.Match(raw, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
            return raw;
        }

        // Presses a button of the item page's add control: the form posts to its action with the item and the amount.
        public async Task<HttpResponseMessage> SetAsync(string action, string slug, int quantity)
        {
            var fields = new Dictionary<string, string>
            {
                ["slug"] = slug,
                ["quantity"] = quantity.ToString(CultureInfo.InvariantCulture),
                ["__RequestVerificationToken"] = _formToken,
            };
            return await SendAsync(new HttpRequestMessage(HttpMethod.Post, action) { Content = new FormUrlEncodedContent(fields) });
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            var cookies = new[] { hamperToken is null ? "" : $"{HamperCookie.Name}={hamperToken}", _antiForgeryCookie }.Where(c => c.Length > 0).ToList();
            if (cookies.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", cookies));
            }

            return client.SendAsync(request);
        }
    }

    private async Task<Visitor> VisitorAsync(DateOnly? delivery)
    {
        var token = Guid.NewGuid().ToString("N");
        await fixture.WithDbAsync(db =>
        {
            db.Hampers.Add(new Hamper { Token = token, BudgetVnd = 500_000, DeliveryDate = delivery, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            return db.SaveChangesAsync();
        });
        return new Visitor(fixture.Client(), token);
    }

    private Task<Dictionary<string, int>> LinesAsync(Visitor visitor) =>
        fixture.WithDbAsync(db => db.HamperItems.Where(l => l.Hamper!.Token == visitor.HamperToken)
            .Select(l => new { l.Product!.Slug, l.Quantity }).ToDictionaryAsync(l => l.Slug, l => l.Quantity));

    // The card next to the photo; the phone bar draws the same control again.
    private static string BuyCard(string html) => Regex.Match(html, @"<div class=""card item-buy"" id=""them"">[\s\S]*?</div>\s*<ul class=""item-promises"">").Value;

    private static string Bar(string html) => Regex.Match(html, @"<div class=""item-bar""[\s\S]*?</div>\s*</div>\s*</main>").Value;

    private static string Text(string html) => WebUtility.HtmlDecode(Regex.Replace(html, @"\s+", " "));

    [Fact]
    public async Task An_unknown_item_is_a_404()
    {
        var response = await new Visitor(fixture.Client(), null).GetAsync("/items/khong-co-mon-nay");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_page_shows_the_item_its_facts_and_the_way_back()
    {
        var html = Text(await new Visitor(fixture.Client(), null).PageAsync());

        Assert.Contains("<title>Trà sen · Peekbox</title>", html);
        Assert.Contains("<li><a href=\"/builder\">Tạo hộp quà</a></li> <li><a href=\"/builder?category=tra-ca-phe#chon-mon\">Trà và cà phê</a></li> <li><span aria-current=\"page\">Trà sen</span></li>", html);
        Assert.Contains("<img class=\"item-photo\" src=\"/images/products/tra-sen.webp\" alt=\"Trà sen\"", html);
        Assert.Contains("<h1 class=\"item-title\">Trà sen</h1>", html);
        Assert.Contains("<span class=\"item-price__value\">180.000đ</span><span class=\"item-price__tax\">Đã gồm thuế</span>", html);
        Assert.Contains("HSD 12/2027</li>", html);
        Assert.Contains("Trà sen đóng hộp giấy tròn.", html);
        foreach (var (label, value) in new[] { ("Danh mục", "Trà và cà phê"), ("Thương hiệu", "Thương hiệu A"), ("Trọng lượng", "100 g"), ("Xuất xứ", "Hà Nội"), ("Hạn sử dụng", "31/12/2027") })
        {
            Assert.Contains($"<div><dt>{label}</dt><dd>{value}</dd></div>", html);
        }
    }

    [Fact]
    public async Task Without_a_saved_budget_and_date_the_page_asks_for_them_instead_of_offering_to_add()
    {
        var html = Text(await new Visitor(fixture.Client(), null).PageAsync());

        Assert.Contains("Chọn ngày giao để kiểm tra hạn dùng của món này.", html);
        Assert.Contains("<a class=\"btn btn--primary item-buy__go\" href=\"/builder\">Đặt ngân sách và ngày giao</a>", BuyCard(html));
        Assert.DoesNotContain("/builder/quantity", html);
    }

    [Fact]
    public async Task A_saved_date_that_has_passed_locks_the_add_control_as_on_the_builder()
    {
        var html = Text(await (await VisitorAsync(TodayInVietnam)).PageAsync());

        Assert.Contains("Lưu ngân sách và ngày giao để thêm món.", BuyCard(html));
        Assert.DoesNotContain("/builder/quantity", html);
    }

    [Fact]
    public async Task With_a_saved_date_the_item_is_checked_and_can_be_added()
    {
        var html = Text(await (await VisitorAsync(Delivery)).PageAsync());

        Assert.Contains("<strong>Đủ hạn cho ngày giao 15/01/2027.</strong> Hạn sử dụng 31/12/2027 còn xa hơn ngày giao cộng 7 ngày dự phòng.", html);
        var card = BuyCard(html);
        Assert.Contains("<div class=\"box-sums__row box-sums__row--left\"><dt>Còn lại</dt><dd>500.000đ</dd></div>", card);
        Assert.Contains("action=\"/builder/quantity?from=item\"", card);
        Assert.Contains("aria-label=\"Thêm Trà sen vào hộp\" data-qty-step=\"add\">Thêm vào hộp</button>", card);
        Assert.Contains("data-qty-step=\"add\">Thêm vào hộp</button>", Bar(html));   // the phone bar has the same control
    }

    // A candle has no expiry date: the facts and the table say so, and the note explains why nothing is checked.
    [Fact]
    public async Task An_item_with_no_expiry_date_says_so_instead_of_checking_the_date()
    {
        var html = Text(await (await VisitorAsync(Delivery)).PageAsync("nen-thom"));

        Assert.Contains("Không có hạn dùng</li>", html);
        Assert.Contains("<div><dt>Hạn sử dụng</dt><dd>Không có hạn dùng</dd></div>", html);
        Assert.Contains("Món này không phải đồ ăn nên không có hạn dùng và không cần kiểm tra theo ngày giao.", html);
        Assert.DoesNotContain("Đủ hạn cho ngày giao", html);
        Assert.Contains("aria-label=\"Thêm Nến thơm vào hộp\" data-qty-step=\"add\">Thêm vào hộp</button>", BuyCard(html));
    }

    [Fact]
    public async Task Adding_from_the_item_page_comes_back_to_it_with_the_pill()
    {
        var visitor = await VisitorAsync(Delivery);
        await visitor.PageAsync();

        var response = await visitor.SetAsync("/builder/quantity?from=item", "tra-sen", 1);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/items/tra-sen#them", response.Headers.Location?.OriginalString);
        Assert.Equal(new Dictionary<string, int> { ["tra-sen"] = 1 }, await LinesAsync(visitor));

        var card = BuyCard(Text(await visitor.PageAsync()));
        Assert.Contains("<span class=\"qty__count\">1</span>", card);
        Assert.Contains("name=\"quantity\" value=\"0\" aria-label=\"Bớt một Trà sen\"", card);
        Assert.Contains("<span>Đã có trong hộp</span> <strong>180.000đ</strong>", card);
        Assert.Contains("<dt>Còn lại</dt><dd>320.000đ</dd>", card);
        Assert.DoesNotContain(">Thêm vào hộp</button>", card);

        await visitor.SetAsync("/builder/quantity?from=item", "tra-sen", 0);
        Assert.Empty(await LinesAsync(visitor));
    }

    // "from" names a place (ReturnTo), never an address: anything else is refused, so it cannot send anyone elsewhere.
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("elsewhere")]
    public async Task Any_other_from_is_refused(string from)
    {
        var visitor = await VisitorAsync(Delivery);
        await visitor.PageAsync();

        var response = await visitor.SetAsync("/builder/quantity?from=" + Uri.EscapeDataString(from), "tra-sen", 1);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task An_item_out_of_date_for_the_delivery_date_says_so_and_cannot_be_added()
    {
        var html = Text(await (await VisitorAsync(Delivery)).PageAsync("banh-quy-bo"));   // expires 10/01/2027

        Assert.Contains("<strong>Không đủ hạn cho ngày giao 15/01/2027.</strong> Hạn sử dụng 10/01/2027 không còn xa hơn ngày giao cộng 7 ngày dự phòng.", html);
        Assert.Contains("disabled>Không chọn được cho ngày giao này</button>", BuyCard(html));
    }

    [Fact]
    public async Task A_sold_out_item_still_has_its_page_and_says_it_is_sold_out()
    {
        var stockBefore = await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "tra-sen").Select(p => p.Stock).SingleAsync());
        await SetStockAsync("tra-sen", 0);
        try
        {
            var html = Text(await (await VisitorAsync(Delivery)).PageAsync());

            Assert.Contains("<strong>Hết hàng.</strong> Món này tạm thời không đặt được.", html);
            Assert.Contains("disabled>Hết hàng</button>", BuyCard(html));
        }
        finally
        {
            await SetStockAsync("tra-sen", stockBefore);
        }
    }

    [Fact]
    public async Task On_the_builder_the_photo_and_the_name_link_to_the_item_page_and_only_the_name_is_in_the_tab_order()
    {
        var visitor = await VisitorAsync(Delivery);
        var response = await visitor.GetAsync("/builder");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<a class=\"item-card__photo\" href=\"/items/tra-sen\" tabindex=\"-1\" aria-hidden=\"true\"><img class=\"item-card__img\"", html);
        Assert.Contains("<h3 class=\"item-card__name\"><a class=\"item-card__link\" href=\"/items/tra-sen\">", html);
    }

    [Fact]
    public async Task Every_state_of_the_page_is_well_formed()
    {
        BuilderItemsTests.AssertWellFormed("no hamper", await new Visitor(fixture.Client(), null).PageAsync());
        var visitor = await VisitorAsync(Delivery);
        BuilderItemsTests.AssertWellFormed("can add", await visitor.PageAsync());
        await visitor.SetAsync("/builder/quantity?from=item", "tra-sen", 1);
        BuilderItemsTests.AssertWellFormed("in the box", await visitor.PageAsync());
        BuilderItemsTests.AssertWellFormed("out of date", await visitor.PageAsync("banh-quy-bo"));
    }

    private Task SetStockAsync(string slug, int stock) =>
        fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == slug).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, stock)));

    // ---- F29: ingredients, allergens, storage and the date they were read through (placeholders) ----

    [Fact]
    public async Task The_page_has_ingredients_allergens_storage_and_the_date_with_a_note_that_they_are_samples()
    {
        var html = Text(await new Visitor(fixture.Client(), null).PageAsync());

        Assert.Contains("<h3 class=\"item-about__sub\">Thành phần và bảo quản</h3>", html);
        Assert.Contains("<dt>Thành phần</dt><dd>Lá trà hoặc hạt cà phê, có thể ướp hương hoa tự nhiên (tùy loại).</dd>", html);
        Assert.Contains("<dt>Chất gây dị ứng</dt><dd>Không có chất gây dị ứng phổ biến.</dd>", html);
        Assert.Contains("<dt>Bảo quản</dt><dd>Giữ kín, nơi khô ráo, tránh ẩm và mùi mạnh.</dd>", html);
        Assert.Contains("<dt>Cập nhật lần cuối</dt><dd>22/11/2026</dd>", html);
        Assert.Contains("Thông tin mẫu theo nhóm món, chưa phải thông tin riêng của sản phẩm này.", html);
        BuilderItemsTests.AssertWellFormed("item with details", html);
    }

    [Fact]
    public async Task An_item_that_is_not_food_has_no_ingredients_line()
    {
        var slug = await fixture.WithDbAsync(db => db.Products.AsNoTracking().Where(p => p.CategoryId == "qua-khong-an").Select(p => p.Slug).FirstAsync());

        var html = Text(await new Visitor(fixture.Client(), null).PageAsync(slug));

        Assert.DoesNotContain("<dt>Thành phần</dt>", html);
        Assert.Contains("<dt>Chất gây dị ứng</dt>", html);
        Assert.Contains("<dt>Bảo quản</dt>", html);
    }

    [Fact]
    public async Task Every_category_of_the_seed_has_its_own_text_and_the_others_say_nothing_that_could_be_wrong()
    {
        var categories = await fixture.WithDbAsync(db => db.Categories.AsNoTracking().Select(c => c.Id).ToListAsync());

        Assert.NotEmpty(categories);
        Assert.All(categories, id => Assert.Contains(id, ItemDetails.Categories));
        Assert.All(ItemDetails.Categories, id => Assert.False(string.IsNullOrWhiteSpace(ItemDetails.For(id).Allergens + ItemDetails.For(id).Storage)));
        Assert.Equal("Xem trên bao bì khi nhận hàng.", ItemDetails.For("a-new-category").Allergens);
    }
    [Fact]
    public async Task The_page_names_its_item_for_the_recently_viewed_strip_and_starts_with_it_hidden()
    {
        var html = Text(await new Visitor(fixture.Client(), null).PageAsync());

        // The strip is filled by recent.ts from the browser's own list: the server only says which item this is.
        Assert.Matches(@"<section [^>]*data-recent[ >][^>]*data-slug=""tra-sen""[^>]*data-name=""Trà sen""[^>]*data-image=""/images/products/[^""]+""[^>]*data-href-prefix=""/items/""[^>]* hidden>", html);
        Assert.Contains("<h2 class=\"recent__title\" id=\"da-xem\">Đã xem gần đây</h2>", html);
        Assert.Contains("<ul class=\"recent__list\" data-recent-list></ul>", html);
        Assert.Matches(@"<script type=""module"" src=""/js/recent(\.[a-z0-9]+)?\.js", html);   // fingerprinted once the site is published
    }

    [Fact]
    public async Task A_page_that_is_not_an_item_has_no_recently_viewed_strip()
    {
        var response = await new Visitor(fixture.Client(), null).GetAsync("/");

        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-recent", html);
        Assert.DoesNotContain("recent.js", html);
    }
}

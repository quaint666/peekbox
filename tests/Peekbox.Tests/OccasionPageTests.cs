using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Peekbox.Web.Data;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// The occasion pages and "Dùng hộp này" (F15, decision 2026-11-17). Addresses and text are written out on purpose: a
// test that read SiteLinks or hampers.json would agree with a typo in them.
[Collection(DatabaseCollection.Name)]
public class OccasionPageTests(PeekboxFixture fixture)
{
    private Visitor NewVisitor() => new(fixture.Client());

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    // A visitor whose box holds one item, with a budget and the test delivery date saved.
    private async Task<Visitor> VisitorWithBoxAsync(string budget)
    {
        var visitor = NewVisitor();
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.PostAsync("/builder", new() { ["budget"] = budget, ["deliveryDate"] = Iso(TestDates.Delivery) })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-lai", ["quantity"] = "1" })).StatusCode);
        return visitor;
    }

    private static string Token(HttpResponseMessage response) =>
        Regex.Match(string.Join(";", response.Headers.GetValues("Set-Cookie")), @"peekbox_hamper=([^;]+)").Groups[1].Value;

    private Task<Hamper> HamperAsync(string token) => fixture.WithDbAsync(db => db.Hampers.AsNoTracking()
        .Include(h => h.Items).ThenInclude(l => l.Product).SingleAsync(h => h.Token == token));

    [Theory]
    [InlineData("/dip/tet", "Hộp quà Tết", new[] { "300.000đ", "500.000đ", "800.000đ", "1 triệu", "2 triệu", "5 triệu", "10 triệu" })]
    [InlineData("/dip/20-11", "Hộp quà 20/11", new[] { "300.000đ", "500.000đ", "800.000đ", "1 triệu", "2 triệu" })]
    [InlineData("/dip/phu-nu", "Hộp quà 8/3 và 20/10", new[] { "300.000đ", "500.000đ", "800.000đ", "1 triệu", "2 triệu" })]
    public async Task Each_occasion_page_shows_its_title_and_its_budget_chips_in_order(string path, string title, string[] chips)
    {
        var html = await NewVisitor().PageAsync(path);
        Assert.Contains($"<h1 class=\"occ-hero__title\">{title}</h1>", html);
        var found = Regex.Matches(html, @"<a class=""occ-budgets__chip"" href=""#ngan-sach-\d+"">([^<]+)</a>").Select(m => m.Groups[1].Value);
        Assert.Equal(chips, found);
        // Every chip leads to a row on the page.
        foreach (Match m in Regex.Matches(html, @"href=""#(ngan-sach-\d+)"""))
        {
            Assert.Contains($"id=\"{m.Groups[1].Value}\"", html);
        }
    }

    // The slim bar: where you are, and a menu of the other five occasions (not this one), in the order of the home cards, each
    // with its colour dot. The paragraph and the button are below the photo, not in its frame.
    [Fact]
    public async Task The_slim_bar_has_the_crumb_and_a_menu_of_the_other_five_occasions_and_the_paragraph_is_below_the_photo()
    {
        var html = await NewVisitor().PageAsync("/dip/trung-thu");

        var bar = Regex.Match(html, @"<div class=""occ-bar"">[\s\S]*?</details>").Value;
        Assert.Contains("<a href=\"/\">Trang chủ</a>", bar);
        Assert.Contains("<span aria-current=\"page\">Trung thu</span>", bar);
        var menu = Regex.Matches(bar, @"<a href=""(/dip/[^""]+)""><span class=""occ-dot occ-dot--(\w+)""").Select(m => (m.Groups[1].Value, m.Groups[2].Value));
        Assert.Equal(
            [("/dip/tet", "tet"), ("/dip/valentine", "valentine"), ("/dip/phu-nu", "women"), ("/dip/20-11", "teachers"), ("/dip/giang-sinh", "christmas")],
            menu);

        var frame = Regex.Match(html, @"<div class=""occ-frame"">[\s\S]*?</div>\s*</div>").Value;
        Assert.Contains("occ-hero__title", frame);
        Assert.Contains("occ-hero__chip", frame);
        Assert.DoesNotContain("occ-hero__lead", frame);
        Assert.Contains("<p class=\"occ-hero__lead\">", html);
        Assert.DoesNotContain("style=", html);
    }

    // No repeated id, no reference to a missing id, every picture and form address real (the builder's checker).
    [Fact]
    public async Task Every_occasion_page_is_well_formed_with_and_without_a_box()
    {
        var withBox = await VisitorWithBoxAsync("300000");
        foreach (var path in new[] { "/dip/tet", "/dip/20-11", "/dip/phu-nu" })
        {
            BuilderItemsTests.AssertWellFormed(path, await NewVisitor().PageAsync(path));
            BuilderItemsTests.AssertWellFormed(path + " with a box", await withBox.PageAsync(path));
        }
    }

    [Fact]
    public async Task An_unknown_occasion_is_a_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await NewVisitor().GetAsync("/dip/khong-co")).StatusCode);
    }

    [Fact]
    public async Task A_card_shows_its_total_with_packaging_its_items_and_the_earliest_expiry()
    {
        var html = await NewVisitor().PageAsync("/dip/tet");
        var card = Regex.Match(html, @"<li class=""hamper"" id=""hop-sum-vay""[\s\S]*?</li> <li class=""hamper""").Value;
        // Trà sen 180.000 + Hạt điều 150.000 + Mứt gừng 90.000 + Hộp cứng có nơ 40.000.
        Assert.Contains("<strong>460.000đ</strong>", card);
        Assert.Contains("3 món · Hộp cứng có nơ", card);
        Assert.Contains("Hạn sớm nhất: 03/2027 (Mứt gừng)", card);
    }

    [Fact]
    public async Task Only_the_20_11_womens_and_Valentine_pages_link_to_Bloomy()
    {
        const string bloomy = "href=\"https://bloomy.quaintshomelabdomain.uk\"";
        foreach (var occasion in new[] { "tet", "trung-thu", "giang-sinh" })
        {
            Assert.DoesNotContain(bloomy, await NewVisitor().PageAsync("/dip/" + occasion));
        }

        Assert.Contains(bloomy, await NewVisitor().PageAsync("/dip/20-11"));
        Assert.Contains("Xem hoa 8/3 và 20/10 trên Bloomy", await NewVisitor().PageAsync("/dip/phu-nu"));
        Assert.Contains("Xem hoa Valentine trên Bloomy", await NewVisitor().PageAsync("/dip/valentine"));
    }

    [Fact]
    public async Task Without_a_box_the_button_is_a_form_and_the_page_asks_nothing()
    {
        var html = await NewVisitor().PageAsync("/dip/tet");
        Assert.DoesNotContain("Thay hộp hiện tại?", html);
        Assert.Matches(@"<form method=""post"" action=""/dip/dung-hop""> <input type=""hidden"" name=""slug"" value=""an-khang"">", html);
    }

    [Fact]
    public async Task Using_a_hamper_with_no_box_makes_one_with_its_items_packaging_and_budget()
    {
        var visitor = NewVisitor();
        var response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "tet" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        // No delivery date yet: step 1 of the builder, to pick one.
        Assert.Equal("/builder", response.Headers.Location?.OriginalString);

        var hamper = await HamperAsync(Token(response));
        Assert.Equal(500_000, hamper.BudgetVnd);
        Assert.Null(hamper.DeliveryDate);
        Assert.Equal("hard-bow", hamper.PackagingId);
        Assert.Equal(["hat-dieu-rang-muoi", "mut-gung", "tra-sen"], hamper.Items.Select(l => l.Product!.Slug).Order());
    }

    [Fact]
    public async Task With_items_in_the_box_the_page_asks_first_and_names_the_budget_raise_and_the_kept_date()
    {
        var visitor = await VisitorWithBoxAsync("300000");
        var html = await visitor.PageAsync("/dip/tet");
        var ask = Regex.Match(html, @"<div class=""confirm"" id=""thay-sum-vay""[\s\S]*?</div> </div>").Value;
        Assert.Contains("Hộp của bạn đang có 1 món. Dùng <strong>Hộp Sum Vầy</strong> sẽ thay các món và kiểu đóng gói bằng của hộp này.", ask);
        Assert.Contains("Ngân sách của bạn (300.000đ) thấp hơn hộp này, nên sẽ được nâng lên 500.000đ.", ask);
        Assert.Contains($"Ngày giao {TestDates.Delivery:dd/MM/yyyy} vẫn được giữ.", ask);
        Assert.Contains("autofocus>Giữ hộp của tôi</button>", ask);
        // The card's button only opens the question.
        Assert.Contains("popovertarget=\"thay-sum-vay\">Dùng hộp này</button>", html);
    }

    [Fact]
    public async Task Without_saying_replace_a_box_with_items_is_left_alone()
    {
        var visitor = await VisitorWithBoxAsync("300000");
        var token = Token(await visitor.PostAsync("/builder", new() { ["budget"] = "300000", ["deliveryDate"] = Iso(TestDates.Delivery) }));
        var response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "tet" });
        Assert.Equal("/dip/tet#hop-sum-vay", response.Headers.Location?.OriginalString);

        var hamper = await HamperAsync(token);
        Assert.Equal(["tra-lai"], hamper.Items.Select(l => l.Product!.Slug));
        Assert.Equal(300_000, hamper.BudgetVnd);
    }

    [Fact]
    public async Task Replacing_puts_in_the_hamper_raises_a_lower_budget_and_keeps_the_date()
    {
        var visitor = await VisitorWithBoxAsync("300000");
        var token = Token(await visitor.PostAsync("/builder", new() { ["budget"] = "300000", ["deliveryDate"] = Iso(TestDates.Delivery) }));
        var response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "tet", ["replace"] = "true" });
        Assert.Equal("/builder#chon-mon", response.Headers.Location?.OriginalString);

        var hamper = await HamperAsync(token);
        Assert.Equal(["hat-dieu-rang-muoi", "mut-gung", "tra-sen"], hamper.Items.Select(l => l.Product!.Slug).Order());
        Assert.Equal(500_000, hamper.BudgetVnd);
        Assert.Equal(TestDates.Delivery, hamper.DeliveryDate);
        Assert.Equal("hard-bow", hamper.PackagingId);
    }

    [Fact]
    public async Task A_higher_budget_is_kept_and_quantities_come_from_the_hamper()
    {
        var visitor = await VisitorWithBoxAsync("20000000");
        var token = Token(await visitor.PostAsync("/builder", new() { ["budget"] = "20000000", ["deliveryDate"] = Iso(TestDates.Delivery) }));
        await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "truong-tho", ["from"] = "tet", ["replace"] = "true" });

        var hamper = await HamperAsync(token);
        Assert.Equal(20_000_000, hamper.BudgetVnd);
        Assert.Equal(2, hamper.Items.Single(l => l.Product!.Slug == "yen-sao-100g").Quantity);
    }

    [Fact]
    public async Task A_hamper_with_an_item_short_is_listed_with_its_button_off_and_cannot_be_used()
    {
        await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "yen-sao-50g").ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 0)));
        try
        {
            var visitor = NewVisitor();
            var html = await visitor.PageAsync("/dip/tet");
            Assert.Contains("Yến sào tinh chế 50 g đang hết hàng. Bạn vẫn có thể tự tạo hộp với các món còn lại.", html);
            Assert.Matches(@"<button type=""button"" class=""btn hamper__use"" disabled aria-describedby=""hop-yen-xuan-card-ly-do"">Tạm chưa dùng được</button>", html);

            var response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "yen-xuan", ["from"] = "tet" });
            Assert.Equal("/dip/tet#hop-yen-xuan", response.Headers.Location?.OriginalString);
            Assert.False(response.Headers.Contains("Set-Cookie") && Token(response).Length > 0, "no hamper was made");
        }
        finally
        {
            await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "yen-sao-50g").ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 15)));
        }
    }

    [Fact]
    public async Task The_way_back_comes_from_the_hampers_own_occasions_not_from_the_request()
    {
        var visitor = await VisitorWithBoxAsync("300000");
        var response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "hoa-giay", ["from"] = "https://example.com" });
        // Not from the request: the first of the hamper's own occasions in the site's order (8/3 comes before 20/11).
        Assert.Equal("/dip/phu-nu#hop-hoa-giay", response.Headers.Location?.OriginalString);
        response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "hoa-giay", ["from"] = "20-11" });
        Assert.Equal("/dip/20-11#hop-hoa-giay", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task An_unknown_hamper_is_a_404_and_a_replace_that_is_not_true_or_false_is_a_400()
    {
        var visitor = NewVisitor();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "khong-co" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "an-khang", ["replace"] = "co" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "an-khang" }, withToken: false)).StatusCode);
    }

    [Fact]
    public void Short_budgets_read_in_millions_from_one_million()
    {
        Assert.Equal(["300.000đ", "500.000đ", "800.000đ", "1 triệu", "2 triệu", "5 triệu", "10 triệu"], BudgetTiers.All.Select(Peekbox.Web.Formatting.Vnd.Short));
        Assert.Equal("1.500.000đ", Peekbox.Web.Formatting.Vnd.Short(1_500_000));
    }
}

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Peekbox.Tests;

// The page of one ready hamper, /hop/{slug} (F15, decision 2026-11-20), and the cards that lead to it. Addresses and text are
// written out on purpose: a test that read SiteLinks or hampers.json would agree with a typo in them.
[Collection(DatabaseCollection.Name)]
public class HamperPageTests(PeekboxFixture fixture)
{
    private Visitor NewVisitor() => new(fixture.Client());

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    [Fact]
    public async Task The_page_shows_the_name_the_price_every_item_with_a_link_to_its_page_and_the_total()
    {
        var html = await NewVisitor().PageAsync("/hop/an-khang");
        Assert.Contains("<h1 class=\"hp-title\">Hộp An Khang</h1>", html);
        Assert.Matches(@"<p class=""hp-price""><strong>300\.000đ</strong><span>Đã gồm thuế</span></p>", html);
        Assert.Contains("Biếu đồng nghiệp", html);
        Assert.Contains("Bên trong hộp", html);
        foreach (var slug in new[] { "ca-phe-phin-giay", "keo-dua", "mit-say", "banh-gung" })
        {
            Assert.Contains($"<a href=\"/items/{slug}\">", html);
        }

        Assert.Matches(@"<p class=""hp-total""><span>Tổng \(đã gồm thuế\)</span><strong>300\.000đ</strong></p>", html);
        Assert.Contains("<title>Hộp An Khang · Peekbox</title>", html);
        // Shared on a chat app it has a name, a line and a photo with an absolute address.
        Assert.Matches(@"<meta property=""og:image"" content=""https?://[^""/]+/images/hampers/an-khang\.webp"">", html);
        Assert.Contains("<meta property=\"og:title\" content=\"Hộp An Khang · Peekbox\">", html);
    }

    [Fact]
    public async Task An_unknown_hamper_is_a_404_and_the_page_answers_HEAD()
    {
        var visitor = NewVisitor();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/hop/khong-co")).StatusCode);
        var head = await fixture.Client().SendAsync(new HttpRequestMessage(HttpMethod.Head, "/hop/an-khang"));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
    }

    // "from" picks the breadcrumb. An occasion the hamper is not in, or an address, is not believed: the first occasion is used.
    [Theory]
    [InlineData("/hop/an-khang", "<a href=\"/dip/tet\">Tết</a>")]
    [InlineData("/hop/an-khang?from=tet", "<a href=\"/dip/tet\">Tết</a>")]
    [InlineData("/hop/an-khang?from=valentine", "<a href=\"/dip/tet\">Tết</a>")]
    [InlineData("/hop/an-khang?from=https://example.com", "<a href=\"/dip/tet\">Tết</a>")]
    [InlineData("/hop/an-khang?from=builder", "<a href=\"/builder#goi-y\">Tạo hộp quà</a>")]
    public async Task The_breadcrumb_follows_where_the_visitor_came_from(string path, string crumb)
    {
        var html = await NewVisitor().PageAsync(path);
        var crumbs = Regex.Match(html, @"<nav class=""breadcrumb""[\s\S]*?</nav>").Value;
        Assert.Contains(crumb, crumbs);
        Assert.Contains("<li><span aria-current=\"page\">Hộp An Khang</span></li>", crumbs);
    }

    [Fact]
    public async Task The_other_hampers_of_the_occasion_are_offered_nearest_budget_first_and_never_the_hamper_itself()
    {
        var html = await NewVisitor().PageAsync("/hop/an-khang?from=tet");
        var related = Regex.Match(html, @"<section class=""hp-related""[\s\S]*?</section>").Value;
        Assert.Contains("Cùng dịp Tết", related);
        var links = Regex.Matches(related, @"class=""hp-rel__link"" href=""(/hop/[^""]+)""").Select(m => m.Groups[1].Value).ToList();
        Assert.InRange(links.Count, 1, 6);
        Assert.DoesNotContain(links, l => l.StartsWith("/hop/an-khang"));
        Assert.All(links, l => Assert.EndsWith("?from=tet", l));
    }

    [Fact]
    public async Task Without_a_box_the_button_on_the_page_is_a_form_that_says_it_comes_from_the_page()
    {
        var html = await NewVisitor().PageAsync("/hop/an-khang?from=tet");
        Assert.Matches(@"<form method=""post"" action=""/dip/dung-hop""> <input type=""hidden"" name=""slug"" value=""an-khang""> <input type=""hidden"" name=""from"" value=""tet""> <input type=""hidden"" name=""detail"" value=""true"">", html);
        Assert.DoesNotContain("Thay hộp hiện tại?", html);
        // The phone's bar is there but hidden until the script shows it.
        Assert.Contains("<div class=\"hp-bar\" data-buy-bar hidden>", html);
    }

    // A refusal (the box holds items and the visitor did not say "Thay hộp", or an item is short) comes back to the page the
    // button was on, with the same "from"; the card's own way back is unchanged.
    [Fact]
    public async Task A_refusal_from_the_page_comes_back_to_the_page_and_never_to_an_address_from_the_request()
    {
        var visitor = NewVisitor();
        await visitor.PostAsync("/builder", new() { ["budget"] = "500000", ["deliveryDate"] = Iso(TestDates.Delivery) });
        await visitor.PostAsync("/builder/quantity", new() { ["slug"] = "tra-lai", ["quantity"] = "1" });

        var response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "tet", ["detail"] = "true" });
        Assert.Equal("/hop/sum-vay?from=tet", response.Headers.Location?.OriginalString);
        response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "builder", ["detail"] = "true" });
        Assert.Equal("/hop/sum-vay?from=builder", response.Headers.Location?.OriginalString);
        response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "https://example.com", ["detail"] = "true" });
        Assert.Equal("/hop/sum-vay", response.Headers.Location?.OriginalString);
        response = await visitor.PostAsync("/dip/dung-hop", new() { ["slug"] = "sum-vay", ["from"] = "tet" });
        Assert.Equal("/dip/tet#hop-sum-vay", response.Headers.Location?.OriginalString);

        // With items in the box the page asks first, and the question's form carries the same marker.
        var html = await visitor.PageAsync("/hop/sum-vay?from=tet");
        Assert.Contains("Thay hộp hiện tại?", html);
        Assert.Matches(@"name=""replace"" value=""true""", html);
        Assert.Matches(@"<input type=""hidden"" name=""detail"" value=""true"">\s*<input type=""hidden"" name=""replace"" value=""true"">", html.Replace("\n", " ").Replace("  ", " ").Replace("> <", ">\n<"));
    }

    [Fact]
    public async Task A_hamper_with_an_item_short_says_which_one_has_its_button_off_and_no_bar()
    {
        await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "yen-sao-50g").ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 0)));
        try
        {
            var html = await NewVisitor().PageAsync("/hop/yen-xuan?from=tet");
            Assert.Contains("Yến sào tinh chế 50 g đang hết hàng.", html);
            Assert.Contains("<span class=\"badge badge--danger\">Hết hàng</span>", html);
            Assert.Matches(@"<button type=""button"" class=""btn hamper__use"" disabled aria-describedby=""hop-yen-xuan-trang-ly-do"">Tạm chưa dùng được</button>", html);
            Assert.DoesNotContain("data-buy-bar", html);
        }
        finally
        {
            await fixture.WithDbAsync(db => db.Products.Where(p => p.Slug == "yen-sao-50g").ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 15)));
        }
    }

    // Each card on an occasion page is one link to the hamper's page: the name, stretched over the card by the stylesheet, with
    // the arrow disc and no "Xem từng món" popover any more.
    [Fact]
    public async Task Each_card_on_an_occasion_page_links_to_its_page_and_the_popover_is_gone()
    {
        var html = await NewVisitor().PageAsync("/dip/tet");
        Assert.Contains("<h4 class=\"hamper__name\"><a class=\"hamper__link\" href=\"/hop/an-khang?from=tet\">Hộp An Khang</a></h4>", html);
        Assert.Contains("<span class=\"card-go\" aria-hidden=\"true\">", html);
        Assert.DoesNotContain("Xem từng món", html);
        Assert.DoesNotContain("hamper-items", html);
        // One link to the page per card.
        Assert.Single(Regex.Matches(html, @"href=""/hop/an-khang\?from=tet"""));

        var css = await File.ReadAllTextAsync(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "occasion.css"));
        Assert.Contains(".hamper__link::after { content: ''; position: absolute; inset: 0; }", css);
        Assert.Contains(".hamper { position: relative; }", css);
    }

    [Fact]
    public async Task The_cards_of_the_builders_hamper_strip_link_to_the_page_with_from_builder()
    {
        var visitor = NewVisitor();
        await visitor.PostAsync("/builder", new() { ["budget"] = "500000", ["deliveryDate"] = Iso(TestDates.Delivery) });
        var html = await visitor.PageAsync("/builder");
        var strip = Regex.Match(html, @"<section class=""suggest""[\s\S]*?</section>").Value;
        Assert.Matches(@"<h3 class=""suggest-card__name""><a href=""/hop/[a-z0-9-]+\?from=builder"">", strip);
        Assert.DoesNotContain("href=\"/dip/tet#hop-", strip);
    }
}

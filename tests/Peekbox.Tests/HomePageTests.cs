using System.Net;
using System.Text.RegularExpressions;
using Peekbox.Web.Content;

namespace Peekbox.Tests;

[Collection(DatabaseCollection.Name)]
public class HomePageTests(PeekboxFixture fixture)
{
    private async Task<string> GetHomeAsync()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Razor encodes Vietnamese letters as entities; decode them to compare text.
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Home_is_a_Vietnamese_page_with_the_shared_header_and_footer()
    {
        var html = await GetHomeAsync();

        Assert.Contains("<html lang=\"vi\">", html);
        Assert.Contains("<header class=\"site-header\">", html);
        Assert.Contains("<footer class=\"site-footer\">", html);
        var content = HomeContent.Load(HomeContent.FilePath);
        Assert.Contains($"<h1 class=\"hero__title\">{content.Title}</h1>", html);
        Assert.Contains(content.Lead, html);
    }

    [Fact]
    public async Task Home_has_the_season_banner_above_the_header()
    {
        var html = await GetHomeAsync();

        var banner = html.IndexOf("<p class=\"home-banner\">", StringComparison.Ordinal);
        Assert.InRange(banner, 0, html.IndexOf("<header class=\"site-header\">", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Each_occasion_card_is_one_link_to_the_builder_with_a_decorative_photo_and_a_button_label()
    {
        var html = await GetHomeAsync();

        var cards = Regex.Matches(html, @"<a class=""occasion occasion--(\w+)"" href=""/builder"">([\s\S]*?)</a>").ToList();
        Assert.Equal(["tet", "teachers", "women"], cards.Select(c => c.Groups[1].Value));

        // Written out here on purpose: a test that read home.json would agree with a typo in it.
        string[] buttons = ["Tạo hộp cho Tết", "Tạo hộp cho 20/11", "Tạo hộp cho 8/3"];
        string[] photos = ["/images/occasions/tet.webp", "/images/occasions/20-11.webp", "/images/occasions/8-3.webp"];
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i].Groups[2].Value;
            Assert.Matches($@"<span class=""occasion__button"">{Regex.Escape(buttons[i])}\s*<svg", card);
            var src = Regex.Match(card, @"<img[^>]*\ssrc=""([^""]+)""[^>]*\salt=""""").Groups[1].Value;
            Assert.Equal(photos[i], BuilderItemsTests.Unfingerprinted(src));
            Assert.DoesNotContain("<a ", card);   // a link inside a link is not allowed; the button is only a label
        }
    }

    // The expected links are written out here on purpose. A test that read SiteLinks would agree with a typo in SiteLinks.
    private static readonly (string Href, string Label)[] HeaderLinks =
    [
        ("/", "Trang chủ"), ("/builder", "Tạo hộp quà"), ("/about", "Giới thiệu"), ("/about#cam-ket", "Cam kết"), ("/blog", "Blog"), ("/track", "Tra cứu đơn hàng"),
    ];

    private static readonly (string Href, string Label)[] FooterLinks =
    [
        ("/about", "Giới thiệu"), ("/about#cam-ket", "Cam kết"), ("/blog", "Blog"), ("/track", "Tra cứu đơn hàng"), ("/legal#dieu-khoan", "Điều khoản"), ("/legal#bao-mat", "Chính sách bảo mật"),
    ];

    // The links of one <nav>, in order, as (href, text). Looking inside one nav keeps a link in the footer from counting for the header.
    private static List<(string Href, string Label)> LinksIn(string html, string navClass)
    {
        var nav = Regex.Match(html, $@"<nav class=""{navClass}""[^>]*>([\s\S]*?)</nav>");
        Assert.True(nav.Success, $"no <nav class=\"{navClass}\"> on the page");
        return Regex.Matches(nav.Groups[1].Value, @"<a href=""([^""]*)""[^>]*>([^<]*)</a>")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
    }

    // The footer has its links in two columns (two <nav> elements with the same class); this joins them in page order.
    private static List<(string Href, string Label)> LinksInAll(string html, string navClass)
    {
        var navs = Regex.Matches(html, $@"<nav class=""{navClass}""[^>]*>([\s\S]*?)</nav>");
        Assert.True(navs.Count > 0, $"no <nav class=\"{navClass}\"> on the page");
        return navs.SelectMany(nav => Regex.Matches(nav.Groups[1].Value, @"<a href=""([^""]*)""[^>]*>([^<]*)</a>")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value))).ToList();
    }

    // CSS hides one of the two logos, and a hidden image's alt text is not read: the name is on the link, so it has
    // one in both themes.
    [Fact]
    public async Task The_logo_link_has_its_name_in_both_themes()
    {
        var html = await GetHomeAsync();

        Assert.Contains("<a class=\"site-header__brand\" href=\"/\" aria-label=\"Peekbox, trang chủ\">", html);
        Assert.Matches(@"<img class=""logo logo--light"" src=""[^""]+"" alt="""" width=""110"" height=""34"">", html);
        Assert.Matches(@"<img class=""logo logo--dark"" src=""[^""]+"" alt="""" width=""110"" height=""34"">", html);
    }

    [Fact]
    public async Task The_header_has_the_six_menu_links_in_order_and_marks_the_current_page()
    {
        var html = await GetHomeAsync();

        Assert.Equal(HeaderLinks, LinksIn(html, "site-nav"));
        Assert.Single(Regex.Matches(html, "aria-current=\"page\""));
        Assert.Contains("<a href=\"/\" aria-current=\"page\">Trang chủ</a>", html);
    }

    [Fact]
    public async Task The_footer_has_the_five_links_in_order()
    {
        var html = await GetHomeAsync();

        Assert.Equal(FooterLinks, LinksInAll(html, "site-footer__links"));
    }

    [Fact]
    public async Task The_cart_link_goes_to_the_cart()
    {
        var html = await GetHomeAsync();

        Assert.Contains("<a class=\"cart-link\" href=\"/cart\"", html);
    }
}

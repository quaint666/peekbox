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

    // The expected links are written out here on purpose. A test that read SiteLinks would agree with a typo in SiteLinks.
    private static readonly (string Href, string Label)[] HeaderLinks =
    [
        ("/", "Trang chủ"), ("/builder", "Tạo hộp quà"), ("/about", "Giới thiệu"), ("/about#cam-ket", "Cam kết"), ("/blog", "Blog"),
    ];

    private static readonly (string Href, string Label)[] FooterLinks =
    [
        ("/about", "Giới thiệu"), ("/about#cam-ket", "Cam kết"), ("/blog", "Blog"), ("/legal#dieu-khoan", "Điều khoản"), ("/legal#bao-mat", "Chính sách bảo mật"),
    ];

    // The links of one <nav>, in order, as (href, text). Looking inside one nav keeps a link in the footer from counting for the header.
    private static List<(string Href, string Label)> LinksIn(string html, string navClass)
    {
        var nav = Regex.Match(html, $@"<nav class=""{navClass}""[^>]*>([\s\S]*?)</nav>");
        Assert.True(nav.Success, $"no <nav class=\"{navClass}\"> on the page");
        return Regex.Matches(nav.Groups[1].Value, @"<a href=""([^""]*)""[^>]*>([^<]*)</a>")
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
    }

    [Fact]
    public async Task The_header_has_the_five_menu_links_in_order_and_marks_the_current_page()
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

        Assert.Equal(FooterLinks, LinksIn(html, "site-footer__links"));
    }

    [Fact]
    public async Task The_cart_link_goes_to_the_cart()
    {
        var html = await GetHomeAsync();

        Assert.Contains("<a class=\"cart-link\" href=\"/cart\"", html);
    }
}

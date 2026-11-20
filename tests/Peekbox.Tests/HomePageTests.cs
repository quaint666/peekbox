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
    public async Task Home_has_the_season_banner_above_the_header_with_a_link_to_the_season_page()
    {
        var html = await GetHomeAsync();

        var banner = html.IndexOf("<div class=\"home-banner home-banner--tet\">", StringComparison.Ordinal);
        Assert.InRange(banner, 0, html.IndexOf("<header class=\"site-header\">", StringComparison.Ordinal));
        Assert.Matches(@"<strong>Mùa Tết 2027:</strong> đặt sớm để còn nhiều lựa chọn\s*<a class=""home-banner__go"" href=""/dip/tet"">Xem hộp Tết", html);
    }

    // Written out here on purpose: a test that read home.json or the database would agree with a typo in either.
    // The count and the budget range are worked out from the hampers in the database (ReadyHampers.SummariesAsync).
    private static readonly (string Accent, string Href, string Chip, string Name, string Photo, string Range, string Count)[] Cards =
    [
        ("tet", "/dip/tet", "Mùa Tết 2027", "Tết", "/images/occasions/tet.webp", "300.000đ – 10 triệu", "25 hộp gợi ý"),
        ("valentine", "/dip/valentine", "Ngày Valentine", "Valentine", "/images/occasions/valentine.webp", "300.000đ – 2 triệu", "15 hộp gợi ý"),
        ("women", "/dip/phu-nu", "Ngày Quốc tế Phụ nữ", "8/3", "/images/occasions/8-3.webp", "300.000đ – 2 triệu", "15 hộp gợi ý"),
        ("midautumn", "/dip/trung-thu", "Tết Trung thu", "Trung thu", "/images/occasions/trung-thu.webp", "300.000đ – 2 triệu", "15 hộp gợi ý"),
        ("teachers", "/dip/20-11", "Ngày Nhà giáo Việt Nam", "20/11", "/images/occasions/20-11.webp", "300.000đ – 2 triệu", "15 hộp gợi ý"),
        ("christmas", "/dip/giang-sinh", "Mùa Giáng sinh", "Giáng sinh", "/images/occasions/giang-sinh.webp", "300.000đ – 2 triệu", "15 hộp gợi ý"),
    ];

    [Fact]
    public async Task Each_occasion_card_is_one_link_to_its_page_with_a_decorative_photo_the_chip_the_name_and_the_two_facts()
    {
        var html = await GetHomeAsync();

        var cards = Regex.Matches(html, @"<a class=""occasion occasion--(\w+)"" href=""([^""]+)"">([\s\S]*?)</a>").ToList();
        Assert.Equal(Cards.Select(c => c.Accent), cards.Select(c => c.Groups[1].Value));
        Assert.Equal(Cards.Select(c => c.Href), cards.Select(c => c.Groups[2].Value));
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i].Groups[3].Value;
            var src = Regex.Match(card, @"<img[^>]*\ssrc=""([^""]+)""[^>]*\salt=""""").Groups[1].Value;
            Assert.Equal(Cards[i].Photo, BuilderItemsTests.Unfingerprinted(src));
            Assert.Contains($"<span class=\"occasion__chip\">{Cards[i].Chip}</span>", card);
            Assert.Matches($@"<h3 class=""occasion__name""><span class=""occasion__dot"" aria-hidden=""true""></span>{Regex.Escape(Cards[i].Name)}</h3>", card);
            Assert.Matches($@"<span class=""occasion__go"" aria-hidden=""true"">\s*<svg", card);   // the arrow is decoration: the link's name is its text
            Assert.Contains($"<strong>{Cards[i].Range}</strong>", card);
            Assert.Contains($"{Cards[i].Count}</li>", card);
            Assert.DoesNotContain("<a ", card);   // a link inside a link is not allowed
        }
    }

    // Written out on purpose (see the card test): the six accents in the order of the cards, the count for a phone and
    // the names for a wide screen. The pill is not a list of three any more.
    [Fact]
    public async Task The_pill_has_one_dot_per_occasion_the_count_for_a_phone_and_the_names_for_a_wide_screen()
    {
        var html = await GetHomeAsync();

        var pill = Regex.Match(html, @"<p class=""badge hero__badge"">(.*?)</p>", RegexOptions.Singleline).Groups[1].Value;
        var dots = Regex.Matches(pill, @"hero__dot hero__dot--(\w+)""").Select(m => m.Groups[1].Value);
        Assert.Equal(["tet", "valentine", "women", "midautumn", "teachers", "christmas"], dots);
        Assert.Contains("<span class=\"hero__pill-short\">6 dịp lễ trong năm</span>", pill);
        Assert.Contains("<span class=\"hero__pill-long\">Tết · Valentine · 8/3 · Trung thu · 20/11 · Giáng sinh</span>", pill);
    }

    [Fact]
    public async Task The_hero_is_a_photo_with_its_text_over_it_and_no_inline_style()
    {
        var html = await GetHomeAsync();
        var content = HomeContent.Load(HomeContent.FilePath);

        var hero = Regex.Match(html, @"<section class=""hero"">[\s\S]*?</section>").Value;
        var photo = Regex.Match(hero, @"<img class=""hero__image"" src=""([^""]+)""[^>]*\salt=""([^""]*)""");
        Assert.Equal("/images/hero.webp", BuilderItemsTests.Unfingerprinted(photo.Groups[1].Value));
        Assert.Equal(content.ImageAlt, photo.Groups[2].Value);
        Assert.Contains("<div class=\"container hero__inner\"><div class=\"hero__copy\">", Regex.Replace(hero, @">\s+<", "><"));
        Assert.DoesNotContain("style=", hero);   // the CSP forbids inline styles
    }

    [Fact]
    public async Task The_third_promise_is_the_shield_with_a_check()
    {
        var html = await GetHomeAsync();

        var promises = Regex.Matches(html, @"<span class=""promise__icon"">\s*<svg class=""icon icon--(\S+?)""").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["eye", "wallet", "shield"], promises);
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

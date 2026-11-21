using System.Net;
using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The About page and the terms and privacy page (F54, F55).
[Collection(DatabaseCollection.Name)]
public class AboutLegalPageTests(PeekboxFixture fixture)
{
    private async Task<(HttpStatusCode Status, string Html)> GetAsync(string path)
    {
        var response = await fixture.Client().GetAsync(path);
        // Razor encodes Vietnamese letters as entities; decode them to compare text. Whitespace is collapsed as a browser shows it.
        return (response.StatusCode, Regex.Replace(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), @"\s+", " "));
    }

    [Fact]
    public async Task The_about_page_shows_the_story_the_steps_the_commitments_and_the_questions()
    {
        var (status, html) = await GetAsync("/about");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<title>Giới thiệu Peekbox · Peekbox</title>", html);
        Assert.Contains("<h1 class=\"about-intro__title\" id=\"about-title\">Giới thiệu Peekbox</h1>", html);
        Assert.Contains("<h2 class=\"about__heading\" id=\"about-section-1\">Peekbox hoạt động thế nào</h2>", html);
        Assert.Contains("<h2 class=\"about__heading\" id=\"cam-ket\">Cam kết của chúng tôi</h2>", html);
        Assert.Equal(7, html.Split("class=\"about-card\"").Length - 1);   // four steps and three commitments
        Assert.Contains("<h3 class=\"about-card__title\">Tổng tiền rõ ràng</h3>", html);
        Assert.Equal(6, html.Split("class=\"about-faq__item card\"").Length - 1);
        Assert.Contains("Hạn sử dụng an toàn hoạt động thế nào", html);
        Assert.Contains("Giao hàng và thanh toán", html);
        Assert.Contains("href=\"/builder\">Tạo hộp quà</a>", html);
        // Static files carry a fingerprint in their name (team-packing.54vesfuy65.webp).
        Assert.Matches(@"src=""/images/about/team-packing(\.\w+)?\.webp""", html);
    }

    // The menu and the footer have no "Cam kết" link: the commitments are a part of this page. "Giới thiệu" is the one marked.
    [Fact]
    public async Task There_is_no_Cam_ket_link_in_the_menu_or_the_footer_and_Gioi_thieu_is_marked_on_the_about_page()
    {
        var (_, html) = await GetAsync("/about");
        var menu = Regex.Matches(html, @"<nav class=""site-nav""[\s\S]*?</nav>").Single().Value;
        var footer = Regex.Matches(html, @"<footer class=""site-footer""[\s\S]*?</footer>").Single().Value;

        Assert.Contains("<a href=\"/about\" aria-current=\"page\">Giới thiệu</a>", menu);
        Assert.DoesNotContain("Cam kết", menu);
        Assert.DoesNotContain("Cam kết", footer);
        Assert.Equal(1, menu.Split("aria-current").Length - 1);
    }

    // The home page's button "Xem cam kết" still leads to the commitments on the About page.
    [Fact]
    public async Task The_home_page_button_still_leads_to_the_commitments()
    {
        var (_, html) = await GetAsync("/");

        Assert.Contains("href=\"/about#cam-ket\"", html);
    }

    [Fact]
    public async Task The_legal_page_has_both_texts_one_after_the_other_and_no_box_of_links()
    {
        var (status, html) = await GetAsync("/legal");

        Assert.Equal(HttpStatusCode.OK, status);
        // The box "Trang này" with links to the two texts was removed at the owner's request (2026-11-21).
        Assert.DoesNotContain("legal-toc", html);
        Assert.DoesNotContain("Trang này", html);
        Assert.Contains("<h1 class=\"legal-text__title\" id=\"dieu-khoan\">Điều khoản sử dụng</h1>", html);
        Assert.Contains("<h1 class=\"legal-text__title\" id=\"bao-mat\">Chính sách bảo mật</h1>", html);
        Assert.Equal(20, html.Split("class=\"legal-text__heading\"").Length - 1);
        Assert.Contains("1. Phạm vi áp dụng", html);
        Assert.Contains("6. Cookie và bộ nhớ trình duyệt", html);
    }

    // The links in the footer and the menu (SiteLinks) point at ids this page really has.
    [Theory]
    [InlineData("/about", "cam-ket")]
    [InlineData("/legal", "dieu-khoan")]
    [InlineData("/legal", "bao-mat")]
    public async Task Every_link_to_a_place_on_these_pages_has_its_target(string path, string id)
    {
        var (_, html) = await GetAsync(path);

        Assert.Contains($"id=\"{id}\"", html);
        Assert.Contains($"href=\"{path}#{id}\"", (await GetAsync("/")).Html);
    }
}

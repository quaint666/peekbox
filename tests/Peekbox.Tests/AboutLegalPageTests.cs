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
    public async Task The_about_page_shows_the_story_three_commitments_the_expiry_rule_and_the_strip()
    {
        var (status, html) = await GetAsync("/about");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<title>Giới thiệu Peekbox · Peekbox</title>", html);
        Assert.Contains("<h1 class=\"about-intro__title\" id=\"about-title\">Giới thiệu Peekbox</h1>", html);
        Assert.Contains("<h2 class=\"about__heading\" id=\"cam-ket\">Cam kết của chúng tôi</h2>", html);
        Assert.Equal(3, html.Split("class=\"about-card\"").Length - 1);
        Assert.Contains("<h3 class=\"about-card__title\">Tổng tiền rõ ràng</h3>", html);
        Assert.Contains("Hạn sử dụng an toàn hoạt động thế nào", html);
        Assert.Contains("href=\"/builder\">Tạo hộp quà</a>", html);
        // Static files carry a fingerprint in their name (team-packing.54vesfuy65.webp).
        Assert.Matches(@"src=""/images/about/team-packing(\.\w+)?\.webp""", html);
    }

    // The menu marks one link, not two: "Cam kết" opens the same page and jumps to its heading.
    [Fact]
    public async Task The_about_page_marks_only_Gioi_thieu_as_the_page_in_the_header()
    {
        var (_, html) = await GetAsync("/about");

        Assert.Contains("<a href=\"/about\" aria-current=\"page\">Giới thiệu</a>", html);
        Assert.Contains("<a href=\"/about#cam-ket\">Cam kết</a>", html);
        Assert.Equal(1, Regex.Matches(html, @"<nav class=""site-nav""[\s\S]*?</nav>").Single().Value.Split("aria-current").Length - 1);
    }

    [Fact]
    public async Task The_legal_page_has_both_texts_and_a_box_of_links_to_them()
    {
        var (status, html) = await GetAsync("/legal");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<nav class=\"legal-toc\"", html);
        Assert.Contains("<a href=\"#dieu-khoan\">Điều khoản sử dụng</a>", html);
        Assert.Contains("<a href=\"#bao-mat\">Chính sách bảo mật</a>", html);
        Assert.Contains("<h1 class=\"legal-text__title\" id=\"dieu-khoan\">Điều khoản sử dụng</h1>", html);
        Assert.Contains("<h1 class=\"legal-text__title\" id=\"bao-mat\">Chính sách bảo mật</h1>", html);
        Assert.Equal(12, html.Split("class=\"legal-text__heading\"").Length - 1);
        Assert.Contains("1. Phạm vi áp dụng", html);
        Assert.Contains("6. Cookie", html);
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

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Peekbox.Web.Services;

namespace Peekbox.Tests;

// The shop's phone, email, address and support hours come from one place (the "Shop" settings) and show in the footer of
// every page, in the "Cần giúp?" line and on the 404 page. Nothing is typed in a view.
[Collection(DatabaseCollection.Name)]
public class ShopInfoTests(PeekboxFixture fixture)
{
    private static string Text(string html) => WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " "));

    [Fact]
    public async Task The_footer_of_a_page_shows_the_shops_contact_details_as_links()
    {
        var html = Text(await new Visitor(fixture.Client()).PageAsync("/about"));

        Assert.Contains("<a href=\"tel:0900000001\">0900 000 001</a> <a href=\"mailto:hello@peekbox.example\">hello@peekbox.example</a> <span>15 Lê Lợi, phường Sài Gòn, TP.HCM</span>", html);
        Assert.DoesNotContain("[SỐ ĐIỆN THOẠI]", html);
    }

    // As Bloomy's footer: two round buttons under the brand (the phone, and Zalo from the same settings) and the course-project notice at the end.
    [Fact]
    public async Task The_footer_has_a_phone_button_and_a_Zalo_button_under_the_brand_and_the_project_notice_at_the_end()
    {
        var html = Text(await new Visitor(fixture.Client()).PageAsync("/about"));
        var social = System.Text.RegularExpressions.Regex.Match(html, @"<div class=""site-footer__social"">.*?</div>").Value;

        Assert.Contains("<a href=\"tel:0900000001\" aria-label=\"Gọi điện\">", social);
        Assert.Contains("rel=\"noopener\" aria-label=\"Chat qua Zalo (mở trang mới)\">", social);
        Assert.Contains("href=\"https://zalo.me/", social);
        Assert.Contains("không phải cửa hàng thật", html[html.IndexOf("site-footer__notice", StringComparison.Ordinal)..]);
    }

    [Fact]
    public async Task The_404_page_and_the_footer_follow_the_settings_not_a_copy_in_a_view()
    {
        await using var other = fixture.Factory.WithWebHostBuilder(builder => builder
            .UseSetting("Shop:Phone", "028 7300 1234")
            .UseSetting("Shop:Email", "ho-tro@example.test")
            .UseSetting("Shop:Address", "1 Đường Thử, TP.HCM")
            .UseSetting("Shop:SupportHours", "8:00 – 20:00"));

        var missing = await fixture.Client(other).GetAsync("/khong-co-trang-nay");
        var html = Text(await missing.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("<a href=\"tel:02873001234\">028 7300 1234</a> hoặc <a href=\"mailto:ho-tro@example.test\">ho-tro@example.test</a> · Hỗ trợ 8:00 – 20:00", html);
        Assert.Contains("1 Đường Thử, TP.HCM", html);   // the footer
        Assert.DoesNotContain("0900 000 001", html);
    }

    [Theory]
    [InlineData("Shop:Phone")]
    [InlineData("Shop:Email")]
    [InlineData("Shop:Address")]
    [InlineData("Shop:SupportHours")]
    [InlineData("Shop:ZaloUrl")]
    public void A_missing_setting_stops_the_site_from_starting_with_its_name(string key)
    {
        var settings = new Dictionary<string, string?> { ["Shop:Phone"] = "1", ["Shop:Email"] = "a@b", ["Shop:Address"] = "x", ["Shop:SupportHours"] = "y", ["Shop:ZaloUrl"] = "https://zalo.me/1", [key] = " " };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var error = Assert.Throws<InvalidOperationException>(() => ShopInfo.FromConfiguration(configuration));
        Assert.Contains(key, error.Message);
    }

    [Fact]
    public void The_zalo_label_is_the_link_without_its_scheme()
    {
        Assert.Equal("zalo.me/0900000001", new ShopInfo("p", "a@b", "x", "y", "https://zalo.me/0900000001").ZaloLabel);
        Assert.Equal("zalo.me/1234", new ShopInfo("p", "a@b", "x", "y", "http://zalo.me/1234/").ZaloLabel);
    }

    [Fact]
    public void The_phone_link_keeps_only_the_digits()
    {
        Assert.Equal("tel:0900000001", new ShopInfo("0900 000 001", "a@b", "x", "y", "z").PhoneLink);
        Assert.Equal("tel:02873001234", new ShopInfo("(028) 7300-1234", "a@b", "x", "y", "z").PhoneLink);
    }
}

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Peekbox.Tests;

public class HomePageTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<string> GetHomeAsync()
    {
        var response = await factory.CreateClient().GetAsync("/");
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
        Assert.Contains("<h1>Peekbox</h1>", html);
    }

    [Fact]
    public async Task The_header_has_the_five_menu_links_and_marks_the_current_page()
    {
        var html = await GetHomeAsync();

        foreach (var label in new[] { "Trang chủ", "Tạo hộp quà", "Giới thiệu", "Cam kết", "Blog" })
        {
            Assert.Matches(new Regex($@"<nav class=""site-nav""[^>]*>[\s\S]*>{label}</a>"), html);
        }
        Assert.Single(Regex.Matches(html, "aria-current=\"page\""));
    }
}

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Peekbox.Web.Data;

namespace Peekbox.Tests;

// The season banner (F05, decision 2026-11-20): off unless the setting Banner:Occasion names an occasion, then one line for
// that occasion over the header of the home page and the builder. The text is written out here on purpose: a test that read
// hampers.json would agree with a typo in it.
[Collection(DatabaseCollection.Name)]
public class SeasonBannerTests(PeekboxFixture fixture)
{
    private WebApplicationFactory<Program> WithBanner(string? occasion) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Banner:Occasion", occasion ?? ""));

    private async Task<string> PageAsync(WebApplicationFactory<Program> factory, string path) =>
        await new Visitor(fixture.Client(factory)).PageAsync(path);

    [Theory]
    [InlineData("/")]
    [InlineData("/builder")]
    public async Task With_no_setting_there_is_no_banner(string path)
    {
        Assert.DoesNotContain("season-banner", await new Visitor(fixture.Client()).PageAsync(path));
        await using var empty = WithBanner("");
        Assert.DoesNotContain("season-banner", await PageAsync(empty, path));
    }

    [Theory]
    [InlineData("tet", "tet", "Mùa Tết 2027", "đặt sớm để còn nhiều lựa chọn", "Tết")]
    [InlineData("valentine", "valentine", "Ngày Valentine", "chọn một hộp quà ngọt ngào và ngày giao sớm", "Valentine")]
    [InlineData("phu-nu", "women", "Ngày Phụ nữ", "chọn món quà ý nghĩa cho mẹ, vợ, bạn gái và đồng nghiệp", "8/3 và 20/10")]
    [InlineData("trung-thu", "midautumn", "Tết Trung thu", "bánh và trà cho đêm rằm, đặt sớm để kịp ngày giao", "Trung thu")]
    [InlineData("20-11", "teachers", "Ngày Nhà giáo Việt Nam", "gửi lời tri ân đến thầy cô bằng một hộp quà nhỏ chu đáo", "20/11")]
    [InlineData("giang-sinh", "christmas", "Mùa Giáng sinh", "chọn hộp quà ấm áp và đặt sớm để kịp ngày 25/12", "Giáng sinh")]
    public async Task The_named_occasion_shows_its_chip_its_line_and_a_link_to_its_page_above_the_header(
        string id, string accent, string chip, string text, string name)
    {
        await using var factory = WithBanner(id);
        var html = await PageAsync(factory, "/");

        var banner = html.IndexOf($"<div class=\"season-banner season-banner--{accent}\">", StringComparison.Ordinal);
        Assert.InRange(banner, 0, html.IndexOf("<header class=\"site-header\">", StringComparison.Ordinal));
        Assert.Matches($@"<strong>{Regex.Escape(chip)}:</strong> {Regex.Escape(text)}\s*<a class=""season-banner__go"" href=""/dip/{Regex.Escape(id)}"">Xem hộp {Regex.Escape(name)}", html);
        Assert.Single(Regex.Matches(html, "season-banner season-banner--"));
    }

    [Fact]
    public async Task The_builder_shows_the_same_banner()
    {
        await using var factory = WithBanner("tet");
        var html = await PageAsync(factory, "/builder");
        Assert.Contains("<div class=\"season-banner season-banner--tet\">", html);
        Assert.InRange(html.IndexOf("season-banner--tet", StringComparison.Ordinal), 0, html.IndexOf("<header class=\"site-header\">", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Pages_that_are_not_home_or_the_builder_have_no_banner()
    {
        await using var factory = WithBanner("tet");
        foreach (var path in new[] { "/cart", "/dip/tet", "/track" })
        {
            Assert.DoesNotContain("season-banner", await PageAsync(factory, path));
        }
    }

    // A typo in the setting must not look like "no banner": the app does not start, and says which ids exist.
    [Theory]
    [InlineData("tett")]
    [InlineData("TET")]
    public void A_setting_that_is_not_an_occasion_stops_the_app_at_start(string value)
    {
        using var factory = WithBanner(value);
        var error = Assert.ThrowsAny<Exception>(() => factory.Services);
        Assert.Contains("Banner:Occasion", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("tet, valentine", error.ToString(), StringComparison.Ordinal);
    }

    // Every accent has its colours in components.css, and every occasion's line is there and does not repeat its chip (the
    // banner writes the chip in bold before it).
    [Fact]
    public async Task Every_occasion_has_a_line_and_its_accent_has_a_banner_rule()
    {
        var hampers = SeedData.LoadHampers(Path.Combine(RepoRoot.Path, "content", "data", "hampers.json"));
        var css = await File.ReadAllTextAsync(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "components.css"));

        Assert.Equal(6, hampers.Occasions.Count);
        Assert.All(hampers.Occasions, o =>
        {
            Assert.False(string.IsNullOrWhiteSpace(o.Banner), $"{o.Id} has no banner line");
            Assert.DoesNotContain(o.Chip, o.Banner, StringComparison.Ordinal);
            Assert.Matches($@"\.season-banner--{Regex.Escape(o.Accent)}\s*\{{[^}}]*--banner-bg:[^}}]*--banner-ink:", css);
        });
    }
}

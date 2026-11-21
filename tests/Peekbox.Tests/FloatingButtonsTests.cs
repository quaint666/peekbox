using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// F06 and F07: the contact button and "Lên đầu trang" on every page; the panel shows the shop's contact details (from the
// settings, like the footer). jsdom and the test host cannot see layout; the CSS is read, and how it looks was measured in
// real browsers.
[Collection(DatabaseCollection.Name)]
public class FloatingButtonsTests(PeekboxFixture fixture)
{
    private static string Css(string file) => File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", file));

    [Theory]
    [InlineData("/")]
    [InlineData("/blog")]
    [InlineData("/about")]
    [InlineData("/dip/tet")]
    [InlineData("/items/tra-sen")]
    [InlineData("/no-such-page")]
    public async Task Every_page_has_the_buttons_and_the_contact_panel(string path)
    {
        var response = await new Visitor(fixture.Client()).GetAsync(path);
        var html = System.Net.WebUtility.HtmlDecode(Regex.Replace(await response.Content.ReadAsStringAsync(), @"\s+", " "));

        Assert.Contains("<button type=\"button\" class=\"float-btn float-top\" data-action=\"to-top\" aria-label=\"Lên đầu trang\" hidden>", html);
        Assert.Contains("class=\"float-btn float-contact\" popovertarget=\"lien-he\" aria-label=\"Liên hệ\"", html);
        var panel = Regex.Match(html, @"<div class=""contact-panel"" id=""lien-he"" popover[\s\S]*?</p>\s*</div>").Value;
        Assert.Contains("href=\"https://zalo.me/0900000001\" target=\"_blank\" rel=\"noopener\"><strong>Chat qua Zalo<span class=\"visually-hidden\"> (mở trang mới)</span></strong><span>zalo.me/0900000001</span>", panel);
        Assert.Contains("href=\"tel:0900000001\"><strong>Gọi điện</strong><span>0900 000 001</span>", panel);
        Assert.Contains("href=\"mailto:hello@peekbox.example\"><strong>Gửi email</strong><span>hello@peekbox.example</span>", panel);
        Assert.Contains("<p class=\"contact-panel__hours\">Hỗ trợ 7:00 – 21:30</p>", panel);
    }

    [Fact]
    public async Task The_panel_follows_the_settings()
    {
        await using var other = fixture.Factory.WithWebHostBuilder(b => b
            .UseSetting("Shop:ZaloUrl", "https://zalo.me/1234")
            .UseSetting("Shop:Phone", "028 7300 1234"));
        var html = await new Visitor(fixture.Client(other)).PageAsync("/about");

        Assert.Contains("href=\"https://zalo.me/1234\"", html);
        Assert.Contains("href=\"tel:02873001234\"", html);
    }

    // A bar that sticks to the bottom of a phone screen carries data-bottom-bar, and layout.ts lifts the buttons above it. A new
    // bar that is not marked would have the buttons on top of its own button, so every such bar in the CSS must be marked in a view.
    [Fact]
    public void Every_bar_that_sticks_to_the_bottom_of_a_phone_is_marked_so_the_buttons_sit_above_it()
    {
        var views = Directory.GetFiles(Path.Combine(RepoRoot.WebProject, "Views"), "*.cshtml", SearchOption.AllDirectories).Select(File.ReadAllText).ToList();
        var found = new List<string>();

        foreach (var file in new[] { "builder.css", "cart.css", "checkout.css", "review.css", "hamper.css", "item.css" })
        {
            foreach (Match bar in Regex.Matches(Css(file), @"^\.(?<name>[a-z-]*bar)\s*\{[^}]*(position:\s*sticky|position:\s*fixed)[^}]*bottom:\s*0", RegexOptions.Multiline))
            {
                var name = bar.Groups["name"].Value;
                found.Add(name);
                Assert.Contains(views, view => view.Contains($"class=\"{name}\" data-bottom-bar"));
            }
        }

        // The scan finds the bars it should (a change to the CSS that hid them from it would otherwise pass in silence).
        Assert.Equal(["box-bar", "cart-bar", "checkout-bar", "hp-bar", "item-bar", "review-bar"], found.Order());
    }

    [Fact]
    public void The_buttons_are_no_longer_hidden_on_a_phone_page_with_a_bar()
    {
        Assert.DoesNotMatch(@"\.float-stack \{ display: none; \}", Css("components.css"));
    }

    [Fact]
    public void The_buttons_sit_under_the_menus_and_the_footers_last_line_keeps_clear_of_them()
    {
        var tokens = Css("tokens.css");
        Assert.Matches(@"--z-float:\s*8;", tokens);
        Assert.Matches(@"\.float-stack \{[^}]*z-index: var\(--z-float\)", Css("components.css"));
        Assert.Matches(@"\.site-footer__bottom-inner \{ padding-inline-end: calc\(20px \+ 56px", Css("components.css"));
    }
}

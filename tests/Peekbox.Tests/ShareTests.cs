using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// F31: share on the item page. The Facebook link is a plain link; the two buttons are hidden until share.ts runs.
[Collection(DatabaseCollection.Name)]
public class ShareTests(PeekboxFixture fixture)
{
    [Fact]
    public async Task The_item_page_has_a_facebook_link_and_two_buttons_that_wait_for_the_script()
    {
        var html = await new Visitor(fixture.Client()).PageAsync("/items/tra-sen");

        var block = Regex.Match(html, @"<div class=""item-share""[\s\S]*?</div>").Value;
        Assert.Contains("data-share-url=\"http://localhost/items/tra-sen\"", block);
        Assert.Contains("data-share-title=\"Trà sen · Peekbox\"", block);
        Assert.Contains("href=\"https://www.facebook.com/sharer/sharer.php?u=http%3A%2F%2Flocalhost%2Fitems%2Ftra-sen\" target=\"_blank\" rel=\"noopener noreferrer\"", block);
        Assert.Contains("<button type=\"button\" class=\"item-share__btn\" data-share-copy hidden>", block);
        Assert.Contains("<button type=\"button\" class=\"item-share__btn\" data-share-system hidden>", block);
        Assert.Matches(@"<script type=""module"" src=""/js/share(\.[a-z0-9]+)?\.js", html);   // fingerprinted once the site is published
    }

    [Fact]
    public async Task The_item_page_describes_itself_for_a_shared_link()
    {
        var html = await new Visitor(fixture.Client()).PageAsync("/items/tra-sen");

        Assert.Contains("<meta property=\"og:title\" content=\"Trà sen · Peekbox\">", html);
        Assert.Contains("<meta property=\"og:url\" content=\"http://localhost/items/tra-sen\">", html);
        Assert.Matches(@"<meta property=""og:image"" content=""http://localhost/images/products/tra-sen\.webp", html);
        Assert.Matches(@"<meta name=""description"" content=""[^""]+""", html);
    }

    [Fact]
    public async Task Every_page_carries_the_toast_for_scripts_and_its_status_line_for_screen_readers()
    {
        var html = await new Visitor(fixture.Client()).PageAsync("/about");

        Assert.Contains("<p class=\"toast\" id=\"toast\" popover=\"manual\" aria-hidden=\"true\"></p>", html);
        Assert.Contains("<p class=\"visually-hidden\" id=\"toast-status\" role=\"status\"></p>", html);
    }

    [Fact]
    public async Task The_shared_address_follows_the_host_the_site_is_served_on()
    {
        await using var other = fixture.Factory.WithWebHostBuilder(_ => { });
        var client = fixture.Client(other);
        client.DefaultRequestHeaders.Host = "peekbox.example";
        var html = await new Visitor(client).PageAsync("/items/tra-sen");

        Assert.Contains("data-share-url=\"http://peekbox.example/items/tra-sen\"", html);
    }
}

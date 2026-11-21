using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// Every card with a photo moves the same way (decision 2026-11-21): one lift, one zoom, one duration, all from tokens.css.
// jsdom and the test host cannot see motion; these read the rules, and how it looks was measured in a browser.
public class CardMotionTests
{
    private static string Css(string file) => File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", file));

    private static readonly string[] CardFiles = ["builder.css", "components.css", "hamper.css", "home.css", "occasion.css", "blog.css"];

    [Fact]
    public void The_lift_the_zoom_and_the_duration_are_tokens_and_no_card_writes_its_own()
    {
        var tokens = Css("tokens.css");
        Assert.Matches(@"--card-lift:\s*-2px;", tokens);
        Assert.Matches(@"--card-zoom:\s*1\.04;", tokens);
        Assert.Matches(@"--motion-card:\s*200ms;", tokens);

        foreach (var file in CardFiles)
        {
            var css = Css(file);
            // A card's hover transform is the token, never a number of its own (a card was once 1.05 where the others were 1.04).
            Assert.DoesNotMatch(@":hover[^{]*\{[^}]*translateY\(-?\d", css);
            Assert.DoesNotMatch(@":hover[^{]*\{[^}]*scale\(1\.\d", css);
        }
    }

    [Fact]
    public void Every_kind_of_photo_card_is_in_the_shared_hover_and_entrance_rules()
    {
        var components = Css("components.css");
        foreach (var card in new[] { ".hamper", ".suggest-card", ".blog-card", ".occasion", ".hp-rel__link" })
        {
            Assert.Contains(card + ":hover", components);
        }

        foreach (var photo in new[] { ".hamper__photo", ".suggest-card__photo", ".blog-card__photo", ".occasion__img", ".hp-rel__photo" })
        {
            Assert.Contains(photo, components);
        }

        Assert.Matches(@"\.occasion__img, \.hp-rel__photo \{ animation: photo-in", components);

        // The first four cards come in one after another, on the lists that have a first row to show.
        Assert.Contains(".blog-list__item:nth-child(-n+4) .blog-card", components);
        Assert.Contains(".occasions__item:nth-child(-n+4) .occasion", components);
        Assert.Contains(".hamper:nth-child(-n+4)", components);
    }
}

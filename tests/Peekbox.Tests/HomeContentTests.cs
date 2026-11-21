using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Peekbox.Web.Content;
using Peekbox.Web.Navigation;

namespace Peekbox.Tests;

public class HomeContentTests
{
    private static readonly HomeContent Content = HomeContent.Load(HomeContent.FilePath);

    [Fact]
    public void The_home_text_has_the_blocks_the_design_shows()
    {
        Assert.Equal(3, Content.Promises.Count);
        Assert.Equal(4, Content.Steps.Count);
        Assert.Equal(6, Content.Occasions.Count);
    }

    [Fact]
    public void Every_promise_names_an_icon_that_exists()
    {
        Assert.All(Content.Promises, promise => Assert.True(Icons.Exists(promise.Icon), $"no icon named \"{promise.Icon}\""));
    }

    [Fact]
    public void Every_occasion_accent_has_a_css_rule_that_sets_its_colour()
    {
        // The page turns the accent into a CSS class (.occasion--tet and so on). home.css is the one place that
        // says which accents exist; an accent without a rule would show a card with no chip colour, no dot and no arrow colour.
        var css = File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "home.css"));

        Assert.All(Content.Occasions, o =>
            Assert.True(
                Regex.IsMatch(css, $@"\.occasion--{Regex.Escape(o.Accent)}\s*\{{[^}}]*--occasion-color\s*:"),
                $"home.json has the accent \"{o.Accent}\" but home.css has no .occasion--{o.Accent} rule that sets --occasion-color"));
    }

    // The card colour is the accent in light mode and the lighter ink in dark mode, so it is written in three places of
    // tokens.css (light, and the two dark blocks) and the dark ones must not be forgotten (decision 2026-11-20).
    [Fact]
    public void Every_occasion_accent_has_its_card_colour_in_light_and_in_both_dark_blocks()
    {
        var tokens = File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "tokens.css"));
        var css = File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "home.css"));

        Assert.All(Content.Occasions, o =>
        {
            var token = $"--occasion-card-{o.Accent}";
            Assert.Equal(3, Regex.Matches(tokens, $@"{Regex.Escape(token)}\s*:").Count);
            Assert.Contains($"var({token})", css);
        });
    }

    [Fact]
    public void Every_occasion_photo_exists_in_the_images_folder()
    {
        // The photo is named in home.json; a typo would show a broken image, so the file must be there.
        var folder = Path.Combine(RepoRoot.WebProject, "wwwroot", "images", "occasions");

        Assert.All(Content.Occasions, o =>
            Assert.True(File.Exists(Path.Combine(folder, o.Image)), $"home.json names the photo \"{o.Image}\" but {folder} has no such file"));
    }

    [Fact]
    public void No_home_text_is_empty_or_an_unfilled_placeholder()
    {
        var texts = new List<string>
        {
            Content.Pill, Content.Title, Content.Lead, Content.PrimaryButton, Content.SecondaryButton, Content.ImageAlt,
            Content.PromisesLabel, Content.StepsTitle, Content.StepLabel, Content.OccasionsTitle, Content.OccasionsSubtitle, Content.ClosingText, Content.ClosingButton,
        };
        texts.AddRange(Content.Promises.SelectMany(c => new[] { c.Icon, c.Title, c.Text }));
        texts.AddRange(Content.Steps.SelectMany(c => new[] { c.Title, c.Text }));
        texts.AddRange(Content.Occasions.SelectMany(o => new[] { o.Name, o.Accent, o.Chip, o.Image, o.Text }));

        Assert.All(texts, text =>
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("[", text);
        });
    }

    [Fact]
    public void A_missing_key_stops_the_load_instead_of_leaving_a_null()
    {
        var original = File.ReadAllText(HomeContent.FilePath);
        var keys = JsonNode.Parse(original)!.AsObject().Select(pair => pair.Key).Where(key => !key.StartsWith('_')).ToList();
        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            var node = JsonNode.Parse(original)!.AsObject();
            node.Remove(key);
            Assert.Throws<JsonException>(() => HomeContent.Parse(node.ToJsonString()));
        }
    }

    [Theory]
    [InlineData("promises")]
    [InlineData("steps")]
    [InlineData("occasions")]
    public void A_missing_key_inside_a_card_stops_the_load(string list)
    {
        var node = JsonNode.Parse(File.ReadAllText(HomeContent.FilePath))!.AsObject();
        var firstCard = node[list]!.AsArray()[0]!.AsObject();
        var keys = firstCard.Select(pair => pair.Key).ToList();

        foreach (var key in keys)
        {
            var copy = JsonNode.Parse(node.ToJsonString())!.AsObject();
            copy[list]!.AsArray()[0]!.AsObject().Remove(key);
            Assert.Throws<JsonException>(() => HomeContent.Parse(copy.ToJsonString()));
        }
    }

    [Fact]
    public void The_carousel_dots_and_the_stand_alone_links_are_named_and_big_enough()
    {
        // The dots are ::scroll-marker boxes the browser exposes as buttons; with no text after the slash they have no name
        // (found in the second review). The links must be 24 px tall (WCAG 2.5.8).
        var css = Path.Combine(RepoRoot.WebProject, "wwwroot", "css");
        Assert.Matches(@"\.occasions__item::scroll-marker\s*\{[^}]*content:\s*''\s*/\s*attr\(data-name\)", File.ReadAllText(Path.Combine(css, "home.css")));
        Assert.Matches(@"\.item-card__link\s*\{[^}]*min-height:\s*24px", File.ReadAllText(Path.Combine(css, "builder.css")));
        Assert.Matches(@"\.hamper__link\s*\{[^}]*min-height:\s*24px", File.ReadAllText(Path.Combine(css, "occasion.css")));
    }
}

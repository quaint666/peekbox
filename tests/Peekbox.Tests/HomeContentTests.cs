using System.Text.Json;
using System.Text.Json.Nodes;
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
        Assert.Equal(3, Content.Occasions.Count);
    }

    [Fact]
    public void Every_promise_names_an_icon_that_exists()
    {
        Assert.All(Content.Promises, promise => Assert.False(string.IsNullOrEmpty(Icons.Path(promise.Icon))));
    }

    [Fact]
    public void Every_occasion_accent_is_one_the_page_has_a_colour_for()
    {
        // The page turns the accent into a CSS class (.occasion--tet and so on); an unknown key would show no colour.
        Assert.All(Content.Occasions, o => Assert.Contains(o.Accent, new[] { "tet", "teachers", "women" }));
    }

    [Fact]
    public void No_home_text_is_empty_or_an_unfilled_placeholder()
    {
        var texts = new List<string>
        {
            Content.Pill, Content.Title, Content.Lead, Content.PrimaryButton, Content.SecondaryButton, Content.ImageAlt,
            Content.PromisesLabel, Content.StepsTitle, Content.StepLabel, Content.OccasionsTitle, Content.OccasionsLink, Content.ClosingText, Content.ClosingButton,
        };
        texts.AddRange(Content.Promises.SelectMany(c => new[] { c.Icon, c.Title, c.Text }));
        texts.AddRange(Content.Steps.SelectMany(c => new[] { c.Title, c.Text }));
        texts.AddRange(Content.Occasions.SelectMany(o => new[] { o.Name, o.Accent, o.Text }));

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
}

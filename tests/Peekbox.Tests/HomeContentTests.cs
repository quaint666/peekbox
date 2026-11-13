using Peekbox.Web.Content;

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
    public void No_home_text_is_empty_or_an_unfilled_placeholder()
    {
        var texts = new List<string>
        {
            Content.Pill, Content.Title, Content.Lead, Content.PrimaryButton, Content.SecondaryButton, Content.ImageAlt,
            Content.StepsTitle, Content.OccasionsTitle, Content.OccasionsLink, Content.ClosingText, Content.ClosingButton,
        };
        texts.AddRange(Content.Promises.SelectMany(c => new[] { c.Title, c.Text }));
        texts.AddRange(Content.Steps.SelectMany(c => new[] { c.Title, c.Text }));
        texts.AddRange(Content.Occasions.SelectMany(o => new[] { o.Name, o.Text }));

        Assert.All(texts, text =>
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("[", text);
        });
    }
}

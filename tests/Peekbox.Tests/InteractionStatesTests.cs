using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The rules of design/design-notes.md, "Interaction states", checked in the CSS files themselves. A browser forgives a
// hover rule outside its media block (it then sticks on phones after a tap), so only a test notices.
public class InteractionStatesTests
{
    private sealed record Rule(string File, string Selector, string Body, IReadOnlyList<string> Media);

    private static readonly string CssFolder = Path.Combine(RepoRoot.WebProject, "wwwroot", "css");

    // Every rule of every CSS file, with the @media conditions around it.
    private static List<Rule> Rules()
    {
        var rules = new List<Rule>();
        foreach (var path in Directory.GetFiles(CssFolder, "*.css"))
        {
            var css = Regex.Replace(File.ReadAllText(path), @"/\*[\s\S]*?\*/", "");
            var media = new Stack<string?>();   // null for a plain rule's braces is never pushed; only @media and @font-face blocks
            var start = 0;
            for (var i = 0; i < css.Length; i++)
            {
                if (css[i] == '{')
                {
                    var head = css[start..i].Trim();
                    if (head.StartsWith('@'))
                    {
                        media.Push(head);
                        start = i + 1;
                        continue;
                    }

                    var end = css.IndexOf('}', i);
                    rules.Add(new Rule(Path.GetFileName(path), head, css[(i + 1)..end], media.Where(m => m is not null).Select(m => m!).ToList()));
                    i = end;
                    start = end + 1;
                }
                else if (css[i] == '}')
                {
                    if (media.Count > 0)
                    {
                        media.Pop();
                    }

                    start = i + 1;
                }
            }
        }

        return rules;
    }

    [Fact]
    public void Every_hover_rule_is_inside_a_hover_hover_media_block()
    {
        var outside = Rules().Where(r => r.Selector.Contains(":hover") && !r.Media.Any(m => m.Contains("hover: hover")))
            .Select(r => $"{r.File}: {r.Selector}").ToList();

        Assert.True(outside.Count == 0, "hover outside @media (hover: hover), so it sticks on phones: " + string.Join("; ", outside));
    }

    [Fact]
    public void Movement_on_hover_or_press_is_only_without_reduce_motion()
    {
        var moving = Rules().Where(r => (r.Selector.Contains(":hover") || r.Selector.Contains(":active")) && r.Body.Contains("transform"))
            .Where(r => !r.Media.Any(m => m.Contains("prefers-reduced-motion: no-preference")))
            .Select(r => $"{r.File}: {r.Selector}").ToList();

        Assert.True(moving.Count == 0, "movement that ignores \"reduce motion\": " + string.Join("; ", moving));
    }

    // Fading in, rising, the ring round an added card, a counter growing: every animation is inside "no-preference", so a
    // visitor who asked for less motion sees none (base.css also switches animations off for them, as a second guard).
    [Fact]
    public void Every_animation_is_only_for_visitors_who_have_not_asked_for_less_motion()
    {
        var animated = Rules().Where(r => Regex.IsMatch(r.Body, @"animation(-name)?\s*:\s*(?!none)[^\s;]"))
            .Where(r => !r.Media.Any(m => m.Contains("prefers-reduced-motion: no-preference")))
            .Select(r => $"{r.File}: {r.Selector}").ToList();

        Assert.True(animated.Count == 0, "animation that ignores \"reduce motion\": " + string.Join("; ", animated));
        // The rules this guard is for exist: the card photo's fade is one of them.
        Assert.Contains(Rules(), r => r.File == "builder.css" && r.Selector == ".item-card__img" && r.Body.Contains("animation: card-photo-in var(--motion-photo)"));
    }

    [Fact]
    public void The_hover_and_pressed_colours_are_in_the_light_and_both_dark_blocks()
    {
        var tokens = File.ReadAllText(Path.Combine(CssFolder, "tokens.css"));

        foreach (var name in new[] { "--color-primary-hover", "--color-primary-pressed" })
        {
            Assert.Equal(3, Regex.Matches(tokens, $@"{name}:\s*#[0-9A-Fa-f]{{6}};").Count);
        }
    }

    [Theory]
    [InlineData(".btn--primary", true, true)]
    [InlineData(".btn--outline", true, true)]
    [InlineData(".icon-btn", true, true)]
    [InlineData(".cart-link", true, true)]
    [InlineData(".site-nav a", true, false)]
    [InlineData(".cta__button", true, true)]
    [InlineData(".picker-tile", true, true)]
    [InlineData(".category-control", true, true)]
    public void Each_control_has_its_hover_and_pressed_look(string control, bool hover, bool pressed)
    {
        var selectors = Rules().SelectMany(r => r.Selector.Split(',').Select(s => s.Trim())).ToList();

        Assert.Equal(hover, selectors.Any(s => s.StartsWith(control + ":hover", StringComparison.Ordinal)));
        Assert.Equal(pressed, selectors.Any(s => s.StartsWith(control + ":active", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_text_field_shows_a_hover_border_and_a_focus_border_but_keeps_the_error_border()
    {
        var selectors = Rules().Select(r => r.Selector).ToList();

        Assert.Contains(selectors, s => s.StartsWith(".input:hover", StringComparison.Ordinal) && s.Contains(":not([aria-invalid='true'])"));
        Assert.Contains(selectors, s => s.StartsWith(".input:focus-visible", StringComparison.Ordinal) && s.Contains(":not([aria-invalid='true'])"));
    }
}

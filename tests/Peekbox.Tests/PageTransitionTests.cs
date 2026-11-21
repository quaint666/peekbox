using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// One page fades into the next (decision 2026-11-21): a cross-document view transition. The test host cannot navigate with a
// real browser; what was measured in Chromium (the transition runs, 200 ms, none with "reduce motion") is in the decision.
public class PageTransitionTests
{
    private static string Css(string file) => File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", file));

    [Fact]
    public void Pages_fade_into_each_other_only_for_visitors_who_have_not_asked_for_less_motion()
    {
        var tokens = Css("tokens.css");
        Assert.Matches(@"--motion-page:\s*200ms;", tokens);

        var block = Regex.Match(Css("base.css"), @"@media \(prefers-reduced-motion: no-preference\) \{\s*@view-transition \{ navigation: auto; \}(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(block.Success, "base.css: @view-transition is not inside the no-preference media query");
        Assert.Contains("animation-duration: var(--motion-page)", block.Groups["body"].Value);
        Assert.Contains(".site-header { view-transition-name: site-header; }", block.Groups["body"].Value);   // the header stays put
    }

    [Fact]
    public void The_header_has_no_fixed_position_descendant_a_view_transition_name_would_trap()
    {
        // A named element becomes the containing block of fixed descendants; the header's menus are absolute, never fixed.
        var components = Css("components.css");
        Assert.DoesNotMatch(@"\.(site-nav|site-header)[^{]*\{[^}]*position:\s*fixed", components);
    }
}

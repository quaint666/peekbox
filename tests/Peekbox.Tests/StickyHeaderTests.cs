using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The header stays at the top while the page scrolls (as Bloomy's does). jsdom and the test host cannot see layout, so these
// read the rules that make it work; how it looks was measured in a browser. What they keep: every bar that sticks to the top
// of the screen starts below the header, so a new one cannot slide under it.
public class StickyHeaderTests
{
    private static readonly string CssFolder = Path.Combine(RepoRoot.WebProject, "wwwroot", "css");

    private static string Css(string file) => Regex.Replace(File.ReadAllText(Path.Combine(CssFolder, file)), @"/\*[\s\S]*?\*/", "");

    [Fact]
    public void The_header_is_sticky_above_the_bars_that_stick_under_it()
    {
        Assert.Matches(@"\.site-header\s*\{[^}]*position:\s*sticky;[^}]*top:\s*0;[^}]*z-index:\s*var\(--z-header\);", Css("components.css"));
        Assert.Matches(@"--z-header:\s*20;", Css("tokens.css"));
        // Above the builder's steps and the occasion's budgets (--z-steps), and not below the menus that open from it.
        Assert.Matches(@"--z-steps:\s*2;", Css("tokens.css"));
    }

    [Fact]
    public void The_header_height_is_written_once_with_its_wide_value_beside_the_header_rules()
    {
        Assert.Matches(@"--site-header-height:\s*68px;", Css("tokens.css"));
        Assert.Matches(@"@media \(min-width: 1100px\)\s*\{\s*:root\s*\{\s*--site-header-height:\s*76px;", Css("components.css"));
        // Anchors (#cam-ket, #ngan-sach-…) stop under it, not behind it; and the phone menu opens right under it without a number.
        Assert.Matches(@"scroll-padding-top:\s*calc\(var\(--site-header-height\)", Css("base.css"));
        Assert.DoesNotContain("67px", Css("components.css"));   // the old fixed offset of the phone menu
    }

    [Fact]
    public void Every_bar_that_sticks_to_the_top_starts_below_the_header()
    {
        var found = 0;
        foreach (var path in Directory.GetFiles(CssFolder, "*.css"))
        {
            if (Path.GetFileName(path) == "admin.css")
            {
                continue;   // the admin has no site header: its sidebar sticks to the very top of the screen
            }

            foreach (Match rule in Regex.Matches(Css(Path.GetFileName(path)), @"([^{}]+)\{([^{}]*position:\s*(?:sticky|fixed)[^{}]*)\}"))
            {
                var top = Regex.Match(rule.Groups[2].Value, @"(?<![-\w])top:\s*([^;]+);");
                var selector = rule.Groups[1].Value.Trim();
                if (!top.Success || selector == ".site-header")
                {
                    continue;   // a bar at the bottom, a panel placed by inset, or the header itself
                }

                found++;
                Assert.True(top.Groups[1].Value.Contains("--site-header-height"), $"{Path.GetFileName(path)}: {selector} sticks to the top at {top.Groups[1].Value.Trim()}, which is under the header");
            }
        }

        Assert.True(found >= 6, "the builder's steps, its pinned search, the boxes beside the lists and the occasion's budgets: the check found only " + found);
    }

    [Fact]
    public void The_builder_panel_compacts_on_a_short_desktop_window()
    {
        // With the header (76 px) and the steps bar (64 px) both sticky, the panel on a 800 px high window left 1.4 items
        // of room (measured in Chromium, Firefox and WebKit); this block gives the lines the room back.
        var css = File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "builder.css"));
        var block = Regex.Match(css, @"@media \(min-width: 900px\) and \(max-height: 960px\) \{(?<body>.*?)\n\}", RegexOptions.Singleline);
        Assert.True(block.Success, "builder.css has no short-desktop block");
        Assert.Contains(".box-panel__compact { display: flex; }", block.Groups["body"].Value);
        Assert.Matches(@"\.box-sums__row[^{]*\{ display: none; \}", block.Groups["body"].Value);   // the rows go, the meter (.box-sums__bar) stays
    }
}

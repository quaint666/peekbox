using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// Guards for the rules that broke in Bloomy. Each test protects one rule from
// docs/frontend-plan.md, section 4. If one fails, fix the code, not the test.
public class SeamsTests
{
    private static string Read(string path) => File.ReadAllText(path);

    private static IEnumerable<string> Files(string folder, string pattern) =>
        Directory.GetFiles(Path.Combine(RepoRoot.WebProject, folder), pattern, SearchOption.AllDirectories);

    private static string Name(string file) => Path.GetRelativePath(RepoRoot.WebProject, file);

    [Fact]
    public void Theme_init_js_uses_the_same_storage_key_as_layout_ts()
    {
        var key = Regex.Match(Read(Path.Combine(RepoRoot.WebProject, "Scripts", "layout.ts")), @"THEME_KEY = '([^']+)'").Groups[1].Value;

        Assert.Matches(@"^peekbox\.", key);
        Assert.Contains($"'{key}'", Read(Path.Combine(RepoRoot.WebProject, "wwwroot", "theme-init.js")));
    }

    // Every POST needs the anti-forgery token (a global filter), and a form only carries it when it says so. A form
    // without it posts fine in a test that adds the token by hand, and gives a blank 400 page in a real browser.
    [Fact]
    public void Every_post_form_in_a_view_asks_for_the_anti_forgery_token()
    {
        var views = Files("Views", "*.cshtml").ToList();
        Assert.NotEmpty(views);
        foreach (var file in views)
        {
            var body = Regex.Replace(Read(file), @"@\*[\s\S]*?\*@|<!--[\s\S]*?-->", "");
            foreach (Match form in Regex.Matches(body, @"<form\b[^>]*>", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(form.Value, @"method\s*=\s*""post""", RegexOptions.IgnoreCase))
                {
                    Assert.True(form.Value.Contains("asp-antiforgery=\"true\""), $"{Name(file)}: POST form without asp-antiforgery=\"true\": {form.Value}");
                }
            }
        }
    }

    [Fact]
    public void No_view_has_inline_styles_or_scripts()
    {
        var views = Files("Views", "*.cshtml").ToList();
        Assert.NotEmpty(views);
        foreach (var file in views)
        {
            // Razor comments and HTML comments may mention these words.
            var body = Regex.Replace(Read(file), @"@\*[\s\S]*?\*@|<!--[\s\S]*?-->", "");
            Assert.False(Regex.IsMatch(body, @"\sstyle\s*="), $"{Name(file)}: inline style attribute");
            Assert.False(Regex.IsMatch(body, @"<style[\s>]"), $"{Name(file)}: <style> block");
            Assert.False(Regex.IsMatch(body, @"\son[a-z]+\s*="""), $"{Name(file)}: inline event attribute");
            Assert.False(Regex.IsMatch(body, @"<script(?![^>]*\ssrc=)[^>]*>"), $"{Name(file)}: inline <script>");
        }
    }

    [Fact]
    public void No_TypeScript_writes_HTML_from_a_string()
    {
        foreach (var file in Files("Scripts", "*.ts"))
        {
            var code = Regex.Replace(Read(file), @"/\*[\s\S]*?\*/|(?<!:)//.*$", "", RegexOptions.Multiline);
            Assert.False(Regex.IsMatch(code, @"\b(innerHTML|outerHTML|insertAdjacentHTML)\b"), $"{Name(file)} writes HTML from a string");
        }
    }

    // Found in WebKit (Safari's engine): Back to the builder showed it from the back-forward cache with a box that had
    // been emptied in the cart since. Every page shows the hamper in the header, so layout.ts loads such a page again.
    [Fact]
    public void A_page_restored_from_the_back_forward_cache_is_loaded_again()
    {
        var code = Read(Path.Combine(RepoRoot.WebProject, "Scripts", "layout.ts"));

        Assert.Matches(@"addEventListener\('pageshow',\s*\(event\)\s*=>\s*\{\s*if \(event\.persisted\)\s*\{\s*window\.location\.reload\(\);", code);
    }

    // Found in WebKit and then in Chromium: after "Thêm" in the background the header still said "Giỏ hàng (0)", and (2026-11-20)
    // the step strip stayed on step 2 after the first item. Everything on the builder that shows the hamper is swapped in from
    // the server's page.
    [Fact]
    public void The_builder_swaps_the_header_count_after_a_background_add()
    {
        var code = Read(Path.Combine(RepoRoot.WebProject, "Scripts", "builder.ts"));
        var applyPage = Regex.Match(code, @"function applyPage\(fresh: Document\): void \{([\s\S]*?)\n\}").Groups[1].Value;

        foreach (var part in new[] { "'.box-bar'", "'.builder-steps'", "'.cart-link'", "'#packaging-lock'", "'#dong-goi'" })
        {
            Assert.Contains($"swap({part}, fresh", applyPage);
        }

        // "Hộp của bạn" goes through swapPanel, which swaps it and keeps the phone's sheet open.
        Assert.Contains("swapPanel(fresh)", applyPage);
        Assert.Contains("swap('#hop-cua-ban', fresh)", Regex.Match(code, @"function swapPanel\(fresh: Document\): void \{([\s\S]*?)\n\}").Groups[1].Value);
    }

    [Fact]
    public void The_global_hidden_rule_is_still_in_base_css()
    {
        var css = Read(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "base.css"));

        Assert.Matches(@"\[hidden\]\s*\{\s*display:\s*none\s*!important", css);
    }

    [Fact]
    public void CSS_files_use_variables_for_colour_hex_colours_only_in_tokens_css()
    {
        foreach (var file in Files(Path.Combine("wwwroot", "css"), "*.css").Where(f => !f.EndsWith("tokens.css")))
        {
            var css = Read(file);
            Assert.False(Regex.IsMatch(css, @"#[0-9a-fA-F]{3,8}\b|rgba?\("), $"{Name(file)}: use a colour from tokens.css");
        }
    }

    // Text sizes and layers come from the scale in tokens.css, so a size or a layer is chosen, not typed: 15 copies of
    // "13px grey" and five hand-picked z-index values were found in the first review.
    [Fact]
    public void CSS_files_take_font_sizes_and_layers_from_tokens_css()
    {
        foreach (var file in Files(Path.Combine("wwwroot", "css"), "*.css").Where(f => !f.EndsWith("tokens.css")))
        {
            foreach (Match m in Regex.Matches(Read(file), @"(font-size|z-index):\s*([^;}]+)"))
            {
                // A raw px, rem or em size, or a bare number; clamp(var(--a), 4vw, var(--b)) is fine.
                Assert.False(Regex.IsMatch(m.Groups[2].Value.Trim(), @"\d(px|rem|em)\b|^\d+$"), $"{Name(file)}: {m.Value} (use a variable from tokens.css)");
            }
        }
    }

    // A style nothing uses is dead weight that looks alive (the first review found .muted and a toast built ahead of
    // its milestone). Every class in the CSS must appear in a view, a script or the C# that writes markup; a class
    // made from a name (class="icon icon--@Model") counts through its prefix.
    [Fact]
    public void Every_css_class_is_used_by_a_view_or_a_script()
    {
        var markup = string.Join("\n", Files("Views", "*.cshtml").Concat(Files("Scripts", "*.ts")).Concat(Files("Models", "*.cs")).Select(Read));
        var prefixes = Regex.Matches(markup, @"([a-z][\w-]*--)@").Select(m => m.Groups[1].Value).ToHashSet();
        foreach (var file in Files(Path.Combine("wwwroot", "css"), "*.css"))
        {
            var css = Regex.Replace(Read(file), @"/\*[\s\S]*?\*/", "");
            css = Regex.Replace(css, @"url\([^)]*\)", "");
            foreach (var name in Regex.Matches(css, @"\.([a-zA-Z][\w-]*)").Select(m => m.Groups[1].Value).Distinct())
            {
                var used = Regex.IsMatch(markup, $@"(?<![\w-]){Regex.Escape(name)}(?![\w-])") || prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));
                Assert.True(used, $"{Name(file)}: .{name} is not used by any view or script");
            }
        }
    }

    [Fact]
    public void The_two_dark_blocks_in_tokens_css_have_the_same_values()
    {
        // CSS cannot share one block between "system is dark" and "visitor chose dark",
        // so tokens.css writes the dark values twice. Changing only one copy makes the
        // site look different depending on how dark mode was turned on.
        var css = Read(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "tokens.css"));
        var system = Values(Regex.Match(css, @":root:not\(\[data-theme='light'\]\)\s*\{([\s\S]*?)\n  \}").Groups[1].Value);
        var chosen = Values(Regex.Match(css, @":root\[data-theme='dark'\]\s*\{([\s\S]*?)\n\}").Groups[1].Value);

        Assert.NotEmpty(system);
        Assert.Equal(system, chosen);
    }

    // The scripts find parts of a page by id ('#hop-cua-ban', `#mon-${slug}`). TypeScript cannot read the C#
    // constants, so a renamed id would leave a script quietly finding nothing: every id a script asks for must be set
    // by a view, written there or taken from Navigation/Anchors.cs. An id that ends in ${...} must be a prefix one.
    [Fact]
    public void Every_id_a_script_asks_for_is_set_by_a_view()
    {
        var views = string.Join("\n", Files("Views", "*.cshtml").Select(Read));
        var ids = Regex.Matches(views, @"\sid=""([a-zA-Z][\w-]*)""").Select(m => m.Groups[1].Value).ToHashSet();
        var prefixes = Regex.Matches(views, @"\sid=""([a-zA-Z][\w-]*-)@").Select(m => m.Groups[1].Value).ToHashSet();
        foreach (var field in typeof(Peekbox.Web.Navigation.Anchors).GetFields().Where(f => f.IsLiteral))
        {
            ids.Add((string)field.GetRawConstantValue()!);
        }

        prefixes.Add(Peekbox.Web.Navigation.Anchors.ItemCard(""));

        foreach (var file in Files("Scripts", "*.ts"))
        {
            var script = Read(file);
            var asked = Regex.Matches(script, @"#([a-zA-Z][\w-]*)(\$\{)?").Select(m => (Id: m.Groups[1].Value, Prefix: m.Groups[2].Success))
                .Concat(Regex.Matches(script, @"getElementById\('([^']+)'\)").Select(m => (Id: m.Groups[1].Value, Prefix: false)));
            foreach (var (id, prefix) in asked)
            {
                Assert.True(prefix ? prefixes.Contains(id) : ids.Contains(id), $"{Name(file)} asks for #{id}{(prefix ? "..." : "")}, which no view sets");
            }
        }
    }

    private static SortedDictionary<string, string> Values(string block) =>
        new(Regex.Matches(block, @"(--[\w-]+):\s*([^;]+);").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim()));

    // Links within a page (the budget chips on an occasion page) glide, and only for a visitor who has not asked for less motion.
    [Fact]
    public void Links_within_a_page_scroll_smoothly_only_when_motion_is_welcome()
    {
        var css = Read(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", "base.css"));

        Assert.Matches(@"@media \(prefers-reduced-motion: no-preference\)\s*\{\s*html\s*\{\s*scroll-behavior: smooth;", css);
    }
}

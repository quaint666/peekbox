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
            // White on the primary colour, and the toast, are the allowed exceptions.
            var css = Regex.Replace(Read(file), "#FFFFFF|#1B2620", "");
            Assert.False(Regex.IsMatch(css, @"#[0-9a-fA-F]{3,8}\b"), $"{Name(file)}: use a variable from tokens.css");
        }
    }
}

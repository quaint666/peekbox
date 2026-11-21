using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// A merge once dropped the closing brace of a media query and the rules after it (the toast) silently stopped applying.
// The stylesheets are read by the browser, never compiled, so nothing else would notice.
public class CssSyntaxTests
{
    public static IEnumerable<object[]> Files() =>
        Directory.GetFiles(Path.Combine(RepoRoot.WebProject, "wwwroot", "css"), "*.css").Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Files))]
    public void Every_brace_in_a_stylesheet_is_closed_and_none_is_closed_twice(string file)
    {
        var css = Regex.Replace(File.ReadAllText(Path.Combine(RepoRoot.WebProject, "wwwroot", "css", file)), @"/\*[\s\S]*?\*/", "");
        css = Regex.Replace(css, @"""[^""\n]*""|'[^'\n]*'", "");   // a brace in a string is not a brace

        var depth = 0;
        for (var i = 0; i < css.Length; i++)
        {
            depth += css[i] == '{' ? 1 : css[i] == '}' ? -1 : 0;
            Assert.True(depth >= 0, $"{file}: a closing brace closes nothing (near character {i})");
        }

        Assert.True(depth == 0, $"{file}: {depth} block(s) are never closed");
    }
}

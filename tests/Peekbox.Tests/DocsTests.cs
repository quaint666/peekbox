using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The docs and the content pack against what is built: the first review found a sitemap of .html file names and blog
// posts whose pictures did not exist.
public class DocsTests
{
    // Every page a visitor can open (a GET address, not the error page) is in docs/sitemap.md.
    [Fact]
    public void The_sitemap_lists_every_page()
    {
        var sitemap = File.ReadAllText(Path.Combine(RepoRoot.Path, "docs", "sitemap.md"));
        var pages = RoutesTests.Expected.Where(e => e.Contains("GET")).Select(e => e[..e.IndexOf(' ')]).Where(p => p != "/error");

        foreach (var page in pages)
        {
            Assert.True(Regex.IsMatch(sitemap, $@"\s{Regex.Escape(page)}(\s|$)", RegexOptions.Multiline), $"docs/sitemap.md does not list {page}");
        }
    }

    // A blog post's cover picture is a file the site serves (/images/... under wwwroot).
    [Fact]
    public void Every_blog_cover_exists()
    {
        foreach (var post in Directory.GetFiles(Path.Combine(RepoRoot.Path, "content", "blog"), "*.md"))
        {
            var cover = Regex.Match(File.ReadAllText(post), @"^cover_image:\s*(\S+)", RegexOptions.Multiline).Groups[1].Value;

            Assert.StartsWith("/images/", cover);
            Assert.True(File.Exists(Path.Combine(RepoRoot.WebProject, "wwwroot", cover.TrimStart('/'))), $"{Path.GetFileName(post)}: {cover} does not exist");
        }
    }
}

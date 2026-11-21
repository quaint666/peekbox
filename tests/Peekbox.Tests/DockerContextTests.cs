using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The Docker build copies only what .dockerignore lets through. A file the project links into the app (the csproj's
// <None Include="..\..\content\...">) that the ignore file drops makes the image build or start fail, and nothing else
// notices: the tests run on the whole repository. This happened with the blog posts (**/*.md).
public class DockerContextTests
{
    [Fact]
    public void Every_content_file_the_project_links_in_reaches_the_docker_build()
    {
        var rules = File.ReadAllLines(Path.Combine(RepoRoot.Path, ".dockerignore"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => (Negated: line.StartsWith('!'), Pattern: Pattern(line.TrimStart('!'))))
            .ToList();

        var csproj = File.ReadAllText(Path.Combine(RepoRoot.WebProject, "Peekbox.Web.csproj"));
        var linked = Regex.Matches(csproj, @"<None Include=""\.\.\\\.\.\\(content\\[^""]+)""").Select(m => m.Groups[1].Value.Replace('\\', '/')).ToList();
        Assert.NotEmpty(linked);

        var files = linked.SelectMany(path => Directory.GetFiles(
            Path.Combine(RepoRoot.Path, Path.GetDirectoryName(path)!), Path.GetFileName(path)).Select(file => Path.GetRelativePath(RepoRoot.Path, file).Replace('\\', '/'))).ToList();
        Assert.NotEmpty(files);

        // Like Docker: the last rule that matches decides; a rule matching a folder covers what is inside it.
        foreach (var file in files)
        {
            var parts = file.Split('/');
            var paths = Enumerable.Range(1, parts.Length).Select(n => string.Join('/', parts.Take(n)));
            var ignored = false;
            foreach (var rule in rules)
            {
                if (paths.Any(path => rule.Pattern.IsMatch(path)))
                {
                    ignored = !rule.Negated;
                }
            }

            Assert.False(ignored, $"{file} is linked into the app but .dockerignore leaves it out of the Docker build");
        }
    }

    // A .dockerignore pattern as a regular expression: "**/" is any folders, "**" anything, "*" anything but "/".
    private static Regex Pattern(string pattern) => new("^" + Regex.Escape(pattern)
        .Replace(@"\*\*/", "(.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*") + "$");
}

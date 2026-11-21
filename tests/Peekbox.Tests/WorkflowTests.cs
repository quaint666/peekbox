using System.Text.RegularExpressions;

namespace Peekbox.Tests;

// The CI workflows run third-party actions with the repository's token. A tag can be moved to other code; a commit cannot.
public class WorkflowTests
{
    [Fact]
    public void Every_action_in_the_workflows_is_pinned_to_a_commit()
    {
        var unpinned = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot.Path, ".github", "workflows"), "*.yml"))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                var use = Regex.Match(line, @"^\s*(?:-\s*)?uses:\s*(?<ref>\S+)");
                if (use.Success && !use.Groups["ref"].Value.StartsWith("./") && !Regex.IsMatch(use.Groups["ref"].Value, @"@[0-9a-f]{40}$"))
                {
                    unpinned.Add($"{Path.GetFileName(file)}: {use.Groups["ref"].Value}");
                }
            }
        }

        Assert.True(unpinned.Count == 0, "actions pinned by tag, not by commit: " + string.Join("; ", unpinned));
    }
}

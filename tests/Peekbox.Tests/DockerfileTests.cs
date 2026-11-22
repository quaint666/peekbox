namespace Peekbox.Tests;

// The Dockerfile's "bundle" stage copies only the C# folders of src/Peekbox.Web, so a change to a view, a style or a script
// does not rebuild the migration bundle (about 50 s). The cost is a list. A folder of C# that is missing from it would
// fail the image build with a compile error at best; this test says which folder to add, before CI builds the image.
public class DockerfileTests
{
    private const string Prefix = "src/Peekbox.Web/";

    [Fact]
    public void The_bundle_stage_copies_every_folder_and_root_file_of_C_sharp_in_the_web_project()
    {
        var copied = new HashSet<string>();
        var inBundleStage = false;
        foreach (var line in File.ReadAllLines(Path.Combine(RepoRoot.Path, "Dockerfile")))
        {
            if (line.StartsWith("FROM ", StringComparison.Ordinal))
            {
                inBundleStage = line.EndsWith(" AS bundle", StringComparison.Ordinal);
            }
            else if (inBundleStage && line.StartsWith("COPY ", StringComparison.Ordinal))
            {
                // COPY <sources...> <destination>: every token but the last is a source.
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).SkipLast(1);
                foreach (var source in parts.Where(p => p.StartsWith(Prefix, StringComparison.Ordinal)))
                {
                    copied.Add(source[Prefix.Length..].TrimEnd('/'));
                }
            }
        }
        Assert.NotEmpty(copied);

        var web = RepoRoot.WebProject;
        var skipped = new[] { "bin", "obj", "node_modules" };
        var needed = Directory.GetDirectories(web)
            .Select(Path.GetFileName)
            .Where(d => d is not null && !skipped.Contains(d))
            .Where(d => Directory.EnumerateFiles(Path.Combine(web, d!), "*.cs", SearchOption.AllDirectories).Any())
            .Concat(Directory.GetFiles(web, "*.cs").Select(Path.GetFileName))
            .ToList();

        var missing = needed.Where(n => !copied.Contains(n!)).ToList();
        Assert.True(missing.Count == 0, "Dockerfile, stage \"bundle\": add a COPY for " + string.Join(", ", missing));
    }
}

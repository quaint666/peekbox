namespace Peekbox.Tests;

// Finds the repository folder, so a test can read the project's own files.
public static class RepoRoot
{
    public static string Path { get; } = Find();

    public static string WebProject => System.IO.Path.Combine(Path, "src", "Peekbox.Web");

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "Peekbox.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Peekbox.slnx not found above the test folder.");
    }
}

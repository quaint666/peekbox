using System.Text.RegularExpressions;

namespace Peekbox.Web.Content;

// The About and commitments page (F54). Its text lives in content/pages/about-and-commitments.md (one copy: the Vietnamese
// text, then the English text); the project copies it next to the app as Content/about.md, and it is read once, when the
// app starts. The site is Vietnamese only, so the English part is left unread. One copy is shared by every request, so it
// is read-only. A missing or odd file stops the app at once, with a message that says which part is wrong.
public sealed record AboutContent(
    string Title,
    IReadOnlyList<string> Intro,
    string CommitmentsTitle,
    IReadOnlyList<AboutCommitment> Commitments,
    string ExpiryTitle,
    string ExpiryText,
    string CtaText,
    string CtaButton)
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Content", "about.md");

    public static AboutContent Load(string path) => Parse(File.ReadAllText(path), path);

    // Throws InvalidOperationException if a part is missing.
    public static AboutContent Parse(string markdown, string file)
    {
        var name = Path.GetFileName(file);
        var viet = MarkdownLines.AfterHeader(markdown)
            .SkipWhile(line => line.Trim() != "# Tiếng Việt").Skip(1)
            .TakeWhile(line => line.Trim() != "# English");

        // Sections: a "##" heading and the blocks under it.
        var sections = new List<(string Heading, List<MarkdownBlock> Blocks)>();
        foreach (var block in MarkdownLines.Blocks(viet))
        {
            if (block.Kind == MarkdownKind.Heading2)
            {
                sections.Add((block.Text, []));
            }
            else if (sections.Count > 0)
            {
                sections[^1].Blocks.Add(block);
            }
        }

        if (sections.Count != 3)
        {
            throw new InvalidOperationException($"{name}: the Vietnamese part must have three \"##\" sections (about, commitments, safe expiry); it has {sections.Count}.");
        }

        var (about, commitments, expiry) = (sections[0], sections[1], sections[2]);
        var intro = about.Blocks.Where(b => b.Kind == MarkdownKind.Paragraph).Select(b => b.Text).ToList();
        if (intro.Count == 0)
        {
            throw new InvalidOperationException($"{name}: the first section has no paragraph.");
        }

        var items = commitments.Blocks.Where(b => b.Kind == MarkdownKind.Numbered).Select(b => Commitment(b.Text, name)).ToList();
        if (items.Count == 0)
        {
            throw new InvalidOperationException($"{name}: the commitments section has no numbered items.");
        }

        // "**Khối kêu gọi:** text | button" is the line of the green strip, in the last section.
        var cta = expiry.Blocks.FirstOrDefault(b => b.Kind == MarkdownKind.Paragraph && b.Text.StartsWith("**Khối kêu gọi:**"));
        var parts = cta?.Text["**Khối kêu gọi:**".Length..].Split('|', StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 } || parts.Any(part => part.Length == 0))
        {
            throw new InvalidOperationException($"{name}: the last section needs a line \"**Khối kêu gọi:** text | button\".");
        }

        var expiryText = expiry.Blocks.Where(b => b.Kind == MarkdownKind.Paragraph && b != cta).Select(b => b.Text).ToList();
        if (expiryText.Count != 1)
        {
            throw new InvalidOperationException($"{name}: the safe expiry section must have one paragraph besides the call to action; it has {expiryText.Count}.");
        }

        return new AboutContent(about.Heading, intro, commitments.Heading, items, expiry.Heading, expiryText[0], parts[0], parts[1]);
    }

    // "**Title.** The text." is a title (without its full stop) and a text.
    private static AboutCommitment Commitment(string item, string name)
    {
        var match = Regex.Match(item, @"^\*\*(.+?)\*\*\s*(.+)$");
        return match.Success
            ? new AboutCommitment(match.Groups[1].Value.TrimEnd('.'), match.Groups[2].Value)
            : throw new InvalidOperationException($"{name}: a commitment must look like \"1. **Title.** Text\", not \"{item}\".");
    }
}

public sealed record AboutCommitment(string Title, string Text);

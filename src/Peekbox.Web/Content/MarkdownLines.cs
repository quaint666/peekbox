namespace Peekbox.Web.Content;

// The few Markdown shapes the About page and the legal texts use, cut into blocks: "#", "##" and "###" headings, numbered
// items ("1. ...") and paragraphs (lines up to a blank line, joined by a space). Nothing here makes HTML: a view writes
// each block itself, so no text is ever printed unescaped. The files' header ("---" lines) is skipped.
public static class MarkdownLines
{
    public static IReadOnlyList<MarkdownBlock> Blocks(IEnumerable<string> lines)
    {
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();

        void EndParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new MarkdownBlock(MarkdownKind.Paragraph, string.Join(' ', paragraph)));
                paragraph.Clear();
            }
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                EndParagraph();
            }
            else if (line.StartsWith("### "))
            {
                EndParagraph();
                blocks.Add(new MarkdownBlock(MarkdownKind.Heading3, line[4..].Trim()));
            }
            else if (line.StartsWith("## "))
            {
                EndParagraph();
                blocks.Add(new MarkdownBlock(MarkdownKind.Heading2, line[3..].Trim()));
            }
            else if (line.StartsWith("# "))
            {
                EndParagraph();
                blocks.Add(new MarkdownBlock(MarkdownKind.Heading1, line[2..].Trim()));
            }
            else if (NumberedItem(line) is { } item)
            {
                EndParagraph();
                blocks.Add(new MarkdownBlock(MarkdownKind.Numbered, item));
            }
            else
            {
                paragraph.Add(line);
            }
        }

        EndParagraph();
        return blocks;
    }

    // The lines of a file after its header (the "---" lines at the top).
    public static IEnumerable<string> AfterHeader(string markdown)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length == 0 || lines[0] != "---")
        {
            return lines;
        }

        var end = Array.IndexOf(lines, "---", 1);
        return end < 0 ? lines : lines.Skip(end + 1);
    }

    // "1. text" gives "text"; anything else gives null.
    private static string? NumberedItem(string line)
    {
        var dot = line.IndexOf(". ", StringComparison.Ordinal);
        return dot > 0 && line[..dot].All(char.IsAsciiDigit) ? line[(dot + 2)..].Trim() : null;
    }
}

public enum MarkdownKind { Heading1, Heading2, Heading3, Numbered, Paragraph }

public sealed record MarkdownBlock(MarkdownKind Kind, string Text);

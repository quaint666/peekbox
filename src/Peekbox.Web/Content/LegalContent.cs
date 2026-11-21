namespace Peekbox.Web.Content;

// The terms of use and the privacy policy (F55). Their text lives in content/legal/ (terms.vi.md and privacy.vi.md; the
// English files are not used, the site is Vietnamese only); the project copies the Vietnamese ones next to the app as
// Content/legal/, and they are read once, when the app starts. One copy is shared by every request, so it is read-only.
// A missing or odd file stops the app at once.
public sealed record LegalContent(LegalDocument Terms, LegalDocument Privacy)
{
    public static string FolderPath => Path.Combine(AppContext.BaseDirectory, "Content", "legal");

    public static LegalContent Load(string folder) => new(
        Parse(File.ReadAllText(Path.Combine(folder, "terms.vi.md")), "terms.vi.md"),
        Parse(File.ReadAllText(Path.Combine(folder, "privacy.vi.md")), "privacy.vi.md"));

    // Throws InvalidOperationException if the file has no title or no section.
    public static LegalDocument Parse(string markdown, string name)
    {
        string? title = null;
        var lead = new List<string>();
        var sections = new List<(string Heading, List<string> Paragraphs)>();

        foreach (var block in MarkdownLines.Blocks(MarkdownLines.AfterHeader(markdown)))
        {
            switch (block.Kind)
            {
                case MarkdownKind.Heading1 when title is null:
                    title = block.Text;
                    break;
                case MarkdownKind.Heading2:
                    sections.Add((block.Text, []));
                    break;
                case MarkdownKind.Paragraph when sections.Count == 0:
                    lead.Add(block.Text);
                    break;
                case MarkdownKind.Paragraph:
                    sections[^1].Paragraphs.Add(block.Text);
                    break;
                default:
                    throw new InvalidOperationException($"{name}: \"{block.Text}\" is not a title, a section heading or a paragraph.");
            }
        }

        if (title is null)
        {
            throw new InvalidOperationException($"{name}: there is no \"# title\".");
        }

        if (sections.Count == 0 || sections.Any(section => section.Paragraphs.Count == 0))
        {
            throw new InvalidOperationException($"{name}: it needs \"##\" sections and each needs a paragraph.");
        }

        return new LegalDocument(title, lead, sections.Select(section => new LegalSection(section.Heading, section.Paragraphs)).ToList());
    }
}

// Lead is what comes before the first section ("Cập nhật lần cuối: [NGÀY]").
public sealed record LegalDocument(string Title, IReadOnlyList<string> Lead, IReadOnlyList<LegalSection> Sections);

public sealed record LegalSection(string Heading, IReadOnlyList<string> Paragraphs);

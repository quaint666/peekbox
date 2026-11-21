using System.Text.RegularExpressions;

namespace Peekbox.Web.Content;

// The About page (F54). Its text lives in content/pages/about-and-commitments.md (one copy: the Vietnamese text, then the
// English text); the project copies it next to the app as Content/about.md, and it is read once, when the app starts. The
// site is Vietnamese only, so the English part is left unread. One copy is shared by every request, so it is read-only.
// A missing or odd file stops the app at once, with a message that says which part is wrong.
//
// The file decides the page: the first "##" section is the introduction (its heading is the page title, shown beside the
// photo), every other "##" section follows in the file's order, and a line "**Khối kêu gọi:** text | button" in the last
// section is the green strip. A section is one of three kinds, told by what is in it: numbered items ("1. **Title.** Text")
// are cards, "###" questions with their answers are a list of questions, and plain paragraphs are a text box. A heading can
// end in "{#id}" to give the section an address (/about#cam-ket).
public sealed record AboutContent(
    string Title,
    IReadOnlyList<string> Intro,
    IReadOnlyList<AboutSection> Sections,
    string CtaText,
    string CtaButton)
{
    private const string CtaMark = "**Khối kêu gọi:**";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Content", "about.md");

    public static AboutContent Load(string path) => Parse(File.ReadAllText(path), path);

    // Throws InvalidOperationException if a part is missing or odd.
    public static AboutContent Parse(string markdown, string file)
    {
        var name = Path.GetFileName(file);
        var viet = MarkdownLines.AfterHeader(markdown)
            .SkipWhile(line => line.Trim() != "# Tiếng Việt").Skip(1)
            .TakeWhile(line => line.Trim() != "# English");

        var raw = new List<(string Heading, List<MarkdownBlock> Blocks)>();
        foreach (var block in MarkdownLines.Blocks(viet))
        {
            if (block.Kind == MarkdownKind.Heading2)
            {
                raw.Add((block.Text, []));
            }
            else if (raw.Count > 0)
            {
                raw[^1].Blocks.Add(block);
            }
        }

        if (raw.Count < 2)
        {
            throw new InvalidOperationException($"{name}: the Vietnamese part needs an introduction and at least one more \"##\" section; it has {raw.Count}.");
        }

        var intro = raw[0].Blocks.Where(b => b.Kind == MarkdownKind.Paragraph).Select(b => b.Text).ToList();
        if (intro.Count == 0 || raw[0].Blocks.Count != intro.Count)
        {
            throw new InvalidOperationException($"{name}: the first section (\"{raw[0].Heading}\") must be paragraphs only, and at least one.");
        }

        // The green strip is a line of the last section.
        var last = raw[^1].Blocks;
        var cta = last.FirstOrDefault(b => b.Kind == MarkdownKind.Paragraph && b.Text.StartsWith(CtaMark));
        var parts = cta?.Text[CtaMark.Length..].Split('|', StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 } || parts.Any(part => part.Length == 0))
        {
            throw new InvalidOperationException($"{name}: the last section needs a line \"{CtaMark} text | button\".");
        }

        last.Remove(cta!);
        var sections = raw.Skip(1).Select(section => Section(section.Heading, section.Blocks, name)).ToList();
        var ids = sections.Select(s => s.Id).Where(id => id is not null).ToList();
        if (ids.Distinct().Count() != ids.Count)
        {
            throw new InvalidOperationException($"{name}: two sections have the same {{#id}}.");
        }

        return new AboutContent(raw[0].Heading, intro, sections, parts[0], parts[1]);
    }

    private static AboutSection Section(string rawHeading, List<MarkdownBlock> blocks, string name)
    {
        var match = Regex.Match(rawHeading, @"^(.*?)\s*\{#([a-z0-9-]+)\}$");
        var heading = match.Success ? match.Groups[1].Value : rawHeading;
        var id = match.Success ? match.Groups[2].Value : null;

        if (blocks.Count == 0)
        {
            throw new InvalidOperationException($"{name}: the section \"{heading}\" is empty.");
        }

        if (blocks.Any(b => b.Kind == MarkdownKind.Numbered))
        {
            return blocks.All(b => b.Kind == MarkdownKind.Numbered)
                ? new AboutCards(heading, id, blocks.Select(b => Card(b.Text, name)).ToList())
                : throw new InvalidOperationException($"{name}: the section \"{heading}\" mixes numbered items with other text.");
        }

        if (blocks.Any(b => b.Kind == MarkdownKind.Heading3))
        {
            var questions = new List<(string Question, List<string> Answer)>();
            foreach (var block in blocks)
            {
                if (block.Kind == MarkdownKind.Heading3)
                {
                    questions.Add((block.Text, []));
                }
                else if (block.Kind == MarkdownKind.Paragraph && questions.Count > 0)
                {
                    questions[^1].Answer.Add(block.Text);
                }
                else
                {
                    throw new InvalidOperationException($"{name}: in the section \"{heading}\", text must come after a \"###\" question.");
                }
            }

            return questions.All(q => q.Answer.Count > 0)
                ? new AboutFaq(heading, id, questions.Select(q => new AboutQuestion(q.Question, q.Answer)).ToList())
                : throw new InvalidOperationException($"{name}: a question in \"{heading}\" has no answer.");
        }

        return blocks.All(b => b.Kind == MarkdownKind.Paragraph)
            ? new AboutText(heading, id, blocks.Select(b => b.Text).ToList())
            : throw new InvalidOperationException($"{name}: the section \"{heading}\" has something that is not a paragraph.");
    }

    // "**Title.** The text." is a title (without its full stop) and a text.
    private static AboutCard Card(string item, string name)
    {
        var match = Regex.Match(item, @"^\*\*(.+?)\*\*\s*(.+)$");
        return match.Success
            ? new AboutCard(match.Groups[1].Value.TrimEnd('.'), match.Groups[2].Value)
            : throw new InvalidOperationException($"{name}: a numbered item must look like \"1. **Title.** Text\", not \"{item}\".");
    }
}

// One section after the introduction. Id is the address of the section (/about#id), or null.
public abstract record AboutSection(string Heading, string? Id);

// Numbered cards: the steps, the commitments.
public sealed record AboutCards(string Heading, string? Id, IReadOnlyList<AboutCard> Items) : AboutSection(Heading, Id);

public sealed record AboutCard(string Title, string Text);

// A box of paragraphs.
public sealed record AboutText(string Heading, string? Id, IReadOnlyList<string> Paragraphs) : AboutSection(Heading, Id);

// Questions and their answers.
public sealed record AboutFaq(string Heading, string? Id, IReadOnlyList<AboutQuestion> Questions) : AboutSection(Heading, Id);

public sealed record AboutQuestion(string Question, IReadOnlyList<string> Answer);

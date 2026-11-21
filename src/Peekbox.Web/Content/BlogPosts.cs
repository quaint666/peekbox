using System.Text.RegularExpressions;

namespace Peekbox.Web.Content;

// The blog posts (F52, F53). They live in content/blog/ (one copy, one Markdown file per post: a header, the Vietnamese
// text, then the English text); the project copies them next to the app as Content/blog/, and they are read once, when
// the app starts. The site is Vietnamese only, so the English part is left unread. A post is turned into plain parts
// (headings, paragraphs, lists), never into HTML, so a view writes each part itself and nothing is printed unescaped.
// One copy is shared by every request, so it is all read-only. A missing or odd file stops the app at once.
public sealed class BlogPosts
{
    public static string FolderPath => Path.Combine(AppContext.BaseDirectory, "Content", "blog");

    // The posts in the order of their file names (01-, 02-, ...), which is the order of the list.
    public IReadOnlyList<BlogPost> All { get; }

    private BlogPosts(IReadOnlyList<BlogPost> all) => All = all;

    public BlogPost? Find(string slug) => All.FirstOrDefault(post => post.Slug == slug);

    // The posts to read next: the ones of the same category first, then the others, in list order.
    public IReadOnlyList<BlogPost> Related(BlogPost post, int count) => All
        .Where(other => other.Slug != post.Slug)
        .OrderBy(other => other.Category == post.Category ? 0 : 1)
        .Take(count)
        .ToList();

    public static BlogPosts Load(string folder)
    {
        var files = Directory.GetFiles(folder, "*.md").Order(StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            throw new InvalidOperationException($"{folder} has no blog posts.");
        }

        var posts = files.Select(file => Parse(File.ReadAllText(file), file)).ToList();
        var twice = posts.GroupBy(post => post.Slug).FirstOrDefault(group => group.Count() > 1);
        return twice is null ? new BlogPosts(posts) : throw new InvalidOperationException($"Two blog posts have the slug \"{twice.Key}\".");
    }

    // Throws InvalidOperationException if a part is missing; the file name is in the message.
    public static BlogPost Parse(string markdown, string file)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var name = Path.GetFileName(file);

        var header = new Dictionary<string, string>();
        var at = 0;
        if (lines.Length > 0 && lines[0] == "---")
        {
            for (at = 1; at < lines.Length && lines[at] != "---"; at++)
            {
                var colon = lines[at].IndexOf(':');
                if (colon > 0)
                {
                    header[lines[at][..colon].Trim()] = lines[at][(colon + 1)..].Trim();
                }
            }
            at++;
        }

        string Need(string key) => header.TryGetValue(key, out var value) && value.Length > 0
            ? value
            : throw new InvalidOperationException($"{name}: the header has no \"{key}\".");

        // The Vietnamese part: from "# Tiếng Việt" to "# English" (or the end).
        var body = lines.Skip(at).SkipWhile(line => line.Trim() != "# Tiếng Việt").Skip(1).TakeWhile(line => line.Trim() != "# English").ToList();
        if (body.Count == 0)
        {
            throw new InvalidOperationException($"{name}: there is no \"# Tiếng Việt\" part.");
        }

        string? title = null;
        var blocks = new List<BlogBlock>();
        var paragraph = new List<string>();
        List<IReadOnlyList<BlogText>>? bullets = null;

        void EndParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new BlogParagraph(Inline(string.Join(' ', paragraph))));
                paragraph.Clear();
            }
        }

        void EndBullets()
        {
            if (bullets is not null)
            {
                blocks.Add(new BlogList(bullets));
                bullets = null;
            }
        }

        foreach (var raw in body)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                EndParagraph();
                EndBullets();
            }
            else if (line.StartsWith("## "))
            {
                EndParagraph();
                EndBullets();
                title = line[3..].Trim();
            }
            else if (line.StartsWith("### "))
            {
                EndParagraph();
                EndBullets();
                blocks.Add(new BlogHeading(line[4..].Trim()));
            }
            else if (line.StartsWith("- "))
            {
                EndParagraph();
                (bullets ??= []).Add(Inline(line[2..]));
            }
            else
            {
                EndBullets();
                paragraph.Add(line);
            }
        }

        EndParagraph();
        EndBullets();

        if (title is null)
        {
            throw new InvalidOperationException($"{name}: the Vietnamese part has no \"## title\".");
        }

        if (blocks.OfType<BlogParagraph>().FirstOrDefault() is not { } first)
        {
            throw new InvalidOperationException($"{name}: the post has no paragraph.");
        }

        return new BlogPost(Need("slug"), Need("category_vi"), Need("cover_image"), title, string.Concat(first.Text.Select(part => part.Text)), blocks);
    }

    // **bold** is the only inline mark the posts use.
    private static IReadOnlyList<BlogText> Inline(string text) =>
        Regex.Split(text, @"\*\*(.+?)\*\*").Select((part, i) => new BlogText(part, Bold: i % 2 == 1)).Where(part => part.Text.Length > 0).ToList();
}

// One post. Cover is an address under wwwroot (/images/blog/...). The excerpt is the first paragraph, for the list.
public sealed record BlogPost(string Slug, string Category, string Cover, string Title, string Excerpt, IReadOnlyList<BlogBlock> Blocks);

public abstract record BlogBlock;

// A heading inside the post (the post's own title is not a block).
public sealed record BlogHeading(string Text) : BlogBlock;

public sealed record BlogParagraph(IReadOnlyList<BlogText> Text) : BlogBlock;

public sealed record BlogList(IReadOnlyList<IReadOnlyList<BlogText>> Items) : BlogBlock;

// A piece of a line: plain, or bold.
public sealed record BlogText(string Text, bool Bold);

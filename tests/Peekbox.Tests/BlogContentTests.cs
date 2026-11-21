using Peekbox.Web.Content;

namespace Peekbox.Tests;

// The blog posts in content/blog/ (F52, F53): read once at start; a broken file stops the app, so these tests say what
// a good file is.
public class BlogContentTests
{
    private static readonly BlogPosts Posts = BlogPosts.Load(Path.Combine(RepoRoot.Path, "content", "blog"));

    // Written out on purpose: a test that read the files would agree with a typo in them.
    private static readonly string[] Slugs =
    [
        "chon-hop-qua-tet-theo-ngan-sach",
        "doc-han-su-dung-tren-bao-bi",
        "bao-quan-banh-mut-tra-sau-tet",
        "qua-tri-an-thay-co-20-11",
        "chon-qua-8-3-cho-me-va-dong-nghiep",
        "qua-cong-ty-chia-ngan-sach",
    ];

    [Fact]
    public void The_six_posts_are_read_in_the_order_of_their_file_names()
    {
        Assert.Equal(Slugs, Posts.All.Select(post => post.Slug));
    }

    [Fact]
    public void The_first_post_has_its_title_category_and_a_first_paragraph_as_excerpt()
    {
        var post = Posts.Find("chon-hop-qua-tet-theo-ngan-sach")!;

        Assert.Equal("Cách chọn hộp quà Tết theo ngân sách", post.Title);
        Assert.Equal("Mẹo chọn quà", post.Category);
        Assert.StartsWith("Quà Tết đẹp không nhất thiết phải đắt.", post.Excerpt);
        Assert.Equal(["1. Đặt ngân sách cho từng hộp", "2. Chọn đủ nhóm, đừng lặp", "3. Nghĩ đến người nhận", "4. Kiểm tra hạn sử dụng so với ngày giao", "Ví dụ hộp 500.000đ (giá mẫu)"],
            post.Blocks.OfType<BlogHeading>().Select(heading => heading.Text));
    }

    [Fact]
    public void A_list_keeps_its_bold_lead_in()
    {
        var list = Posts.Find("doc-han-su-dung-tren-bao-bi")!.Blocks.OfType<BlogList>().Single();

        Assert.Equal(3, list.Items.Count);
        Assert.Equal(new BlogText("NSX hoặc Ngày sản xuất:", Bold: true), list.Items[0][0]);
        Assert.False(list.Items[0][1].Bold);
    }

    [Fact]
    public void Only_the_Vietnamese_text_is_read_and_no_Markdown_marks_are_left()
    {
        foreach (var post in Posts.All)
        {
            var text = string.Join('\n', post.Blocks.SelectMany(Texts).Append(post.Title).Append(post.Excerpt));

            Assert.DoesNotContain("How to ", text);
            Assert.DoesNotContain("**", text);
            Assert.DoesNotContain("##", text);
            Assert.DoesNotContain("# English", text);
        }
    }

    [Fact]
    public void Every_cover_is_a_photo_the_site_has()
    {
        Assert.All(Posts.All, post =>
        {
            Assert.StartsWith("/images/blog/", post.Cover);
            Assert.True(File.Exists(Path.Combine(RepoRoot.WebProject, "wwwroot", post.Cover.TrimStart('/'))), $"{post.Slug}: no file {post.Cover}");
        });
    }

    [Fact]
    public void The_other_posts_are_two_others_and_start_with_the_same_category()
    {
        var first = Posts.Find("chon-hop-qua-tet-theo-ngan-sach")!;
        var related = Posts.Related(first, 2);

        Assert.Equal(2, related.Count);
        Assert.DoesNotContain(related, post => post.Slug == first.Slug);
        // "Mẹo chọn quà" is also the category of the company-gift post, so it comes first.
        Assert.Equal("qua-cong-ty-chia-ngan-sach", related[0].Slug);
        Assert.Equal("doc-han-su-dung-tren-bao-bi", related[1].Slug);
    }

    [Fact]
    public void A_post_without_a_slug_or_a_title_is_refused_with_the_file_name_in_the_message()
    {
        var noSlug = Assert.Throws<InvalidOperationException>(() => BlogPosts.Parse("---\ncategory_vi: A\ncover_image: /x.webp\n---\n# Tiếng Việt\n## T\n\nP\n", "a.md"));
        var noTitle = Assert.Throws<InvalidOperationException>(() => BlogPosts.Parse("---\nslug: a\ncategory_vi: A\ncover_image: /x.webp\n---\n# Tiếng Việt\n\nP\n", "b.md"));

        Assert.Contains("a.md", noSlug.Message);
        Assert.Contains("slug", noSlug.Message);
        Assert.Contains("b.md", noTitle.Message);
    }

    private static IEnumerable<string> Texts(BlogBlock block) => block switch
    {
        BlogHeading heading => [heading.Text],
        BlogParagraph paragraph => paragraph.Text.Select(part => part.Text),
        BlogList list => list.Items.SelectMany(item => item.Select(part => part.Text)),
        _ => [],
    };
}

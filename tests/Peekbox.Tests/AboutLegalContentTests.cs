using Peekbox.Web.Content;

namespace Peekbox.Tests;

// The About page and the legal texts (F54, F55): read once at start from content/. A broken file stops the app, so these
// tests say what a good file is.
public class AboutLegalContentTests
{
    private static readonly AboutContent About = AboutContent.Parse(File.ReadAllText(Path.Combine(RepoRoot.Path, "content", "pages", "about-and-commitments.md")), "about-and-commitments.md");
    private static readonly LegalContent Legal = LegalContent.Load(Path.Combine(RepoRoot.Path, "content", "legal"));

    // Written out on purpose: a test that read the files would agree with a typo in them.
    [Fact]
    public void The_about_text_has_its_three_commitments_and_the_green_strip()
    {
        Assert.Equal("Giới thiệu Peekbox", About.Title);
        Assert.Equal("Cam kết của chúng tôi", About.CommitmentsTitle);
        Assert.Equal(["Thông tin đủ và thật", "Tổng tiền rõ ràng", "Không giao món hết hạn"], About.Commitments.Select(c => c.Title));
        Assert.StartsWith("Mỗi món hiển thị thương hiệu", About.Commitments[0].Text);
        Assert.Equal("Hạn sử dụng an toàn hoạt động thế nào", About.ExpiryTitle);
        Assert.StartsWith("Khi bạn chọn ngày giao", About.ExpiryText);
        Assert.Equal("Thử tạo một hộp quà và xem từng món bên trong", About.CtaText);
        Assert.Equal("Tạo hộp quà", About.CtaButton);
        Assert.Equal(2, About.Intro.Count);
    }

    [Fact]
    public void The_two_legal_texts_have_six_numbered_sections_each()
    {
        Assert.Equal("Điều khoản sử dụng", Legal.Terms.Title);
        Assert.Equal("Chính sách bảo mật", Legal.Privacy.Title);
        Assert.Equal(6, Legal.Terms.Sections.Count);
        Assert.Equal(6, Legal.Privacy.Sections.Count);
        Assert.Equal("1. Phạm vi áp dụng", Legal.Terms.Sections[0].Heading);
        Assert.Equal("1. Thông tin chúng tôi thu thập", Legal.Privacy.Sections[0].Heading);
        Assert.Equal(["Cập nhật lần cuối: [NGÀY]"], Legal.Terms.Lead);
    }

    [Fact]
    public void No_Markdown_marks_are_left_in_any_text()
    {
        var texts = new List<string> { About.Title, About.CommitmentsTitle, About.ExpiryTitle, About.ExpiryText, About.CtaText, About.CtaButton };
        texts.AddRange(About.Intro);
        texts.AddRange(About.Commitments.SelectMany(c => new[] { c.Title, c.Text }));
        foreach (var document in new[] { Legal.Terms, Legal.Privacy })
        {
            texts.Add(document.Title);
            texts.AddRange(document.Lead);
            texts.AddRange(document.Sections.SelectMany(s => s.Paragraphs.Prepend(s.Heading)));
        }

        Assert.All(texts, text =>
        {
            Assert.DoesNotContain("**", text);
            Assert.DoesNotContain("##", text);
            Assert.DoesNotContain("# English", text);
        });
    }

    [Fact]
    public void A_file_with_a_missing_part_is_refused_with_its_name_in_the_message()
    {
        var oneSection = Assert.Throws<InvalidOperationException>(() => AboutContent.Parse("# Tiếng Việt\n\n## A\nText\n", "about.md"));
        var noButton = Assert.Throws<InvalidOperationException>(() => AboutContent.Parse(
            "# Tiếng Việt\n\n## A\nText\n\n## B\n1. **T.** x\n\n## C\nText\n\n**Khối kêu gọi:** only text\n", "about.md"));
        var noTitle = Assert.Throws<InvalidOperationException>(() => LegalContent.Parse("## 1. A\nText\n", "terms.vi.md"));
        var emptySection = Assert.Throws<InvalidOperationException>(() => LegalContent.Parse("# T\n\n## 1. A\n\n## 2. B\nText\n", "terms.vi.md"));

        Assert.Contains("about.md", oneSection.Message);
        Assert.Contains("Khối kêu gọi", noButton.Message);
        Assert.Contains("terms.vi.md", noTitle.Message);
        Assert.Contains("each needs a paragraph", emptySection.Message);
    }
}

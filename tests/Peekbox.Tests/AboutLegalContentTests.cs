using Peekbox.Web.Content;
using Peekbox.Web.Navigation;

namespace Peekbox.Tests;

// The About page and the legal texts (F54, F55): read once at start from content/. A broken file stops the app, so these
// tests say what a good file is.
public class AboutLegalContentTests
{
    private static readonly AboutContent About = AboutContent.Parse(File.ReadAllText(Path.Combine(RepoRoot.Path, "content", "pages", "about-and-commitments.md")), "about-and-commitments.md");
    private static readonly LegalContent Legal = LegalContent.Load(Path.Combine(RepoRoot.Path, "content", "legal"));

    // Written out on purpose: a test that read the files would agree with a typo in them.
    [Fact]
    public void The_about_text_has_the_sections_the_owner_asked_for_in_this_order()
    {
        Assert.Equal("Giới thiệu Peekbox", About.Title);
        Assert.Equal(3, About.Intro.Count);
        Assert.Equal(
        [
            "Peekbox hoạt động thế nào", "Cam kết của chúng tôi", "Các dịp chúng tôi phục vụ", "Giao hàng và thanh toán",
            "Hạn sử dụng an toàn hoạt động thế nào", "Câu hỏi thường gặp",
        ], About.Sections.Select(section => section.Heading));
        Assert.Equal("Thử tạo một hộp quà và xem từng món bên trong", About.CtaText);
        Assert.Equal("Tạo hộp quà", About.CtaButton);
    }

    // The owner's decision of 2026-11-21: the sixth occasion is called "8/3 và 20/10" everywhere.
    [Fact]
    public void The_occasions_are_named_as_everywhere_else()
    {
        var all = string.Join('\n', AboutTexts());

        Assert.Contains("8/3 và 20/10", all);
        Assert.DoesNotContain("Valentine, 8/3,", all);
    }

    [Fact]
    public void The_steps_and_the_commitments_are_numbered_cards_and_the_commitments_have_their_address()
    {
        var steps = Assert.IsType<AboutCards>(About.Sections[0]);
        var commitments = Assert.IsType<AboutCards>(About.Sections[1]);

        Assert.Equal(["Đặt ngân sách và ngày giao", "Chọn món hoặc dùng hộp gợi ý", "Chọn đóng gói và xem lại", "Đặt hàng và theo dõi"], steps.Items.Select(c => c.Title));
        Assert.Equal(["Thông tin đủ và thật", "Tổng tiền rõ ràng", "Không giao món hết hạn"], commitments.Items.Select(c => c.Title));
        Assert.StartsWith("Mỗi món hiển thị thương hiệu", commitments.Items[0].Text);
        Assert.Equal(SiteLinks.CommitmentsAnchor, commitments.Id);
        Assert.Null(steps.Id);
    }

    [Fact]
    public void The_questions_each_have_an_answer()
    {
        var faq = Assert.IsType<AboutFaq>(About.Sections[^1]);

        Assert.Equal(6, faq.Questions.Count);
        Assert.Equal("Tôi có cần tạo tài khoản để đặt hàng không?", faq.Questions[0].Question);
        Assert.All(faq.Questions, q => Assert.NotEmpty(q.Answer));
    }

    [Fact]
    public void Every_section_has_text_and_no_two_share_an_address()
    {
        Assert.All(About.Sections, section => Assert.NotEmpty(section.Heading));
        var ids = About.Sections.Select(section => section.Id).Where(id => id is not null).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void The_two_legal_texts_have_ten_numbered_sections_each()
    {
        Assert.Equal("Điều khoản sử dụng", Legal.Terms.Title);
        Assert.Equal("Chính sách bảo mật", Legal.Privacy.Title);
        Assert.Equal(10, Legal.Terms.Sections.Count);
        Assert.Equal(10, Legal.Privacy.Sections.Count);
        Assert.Equal("1. Phạm vi áp dụng", Legal.Terms.Sections[0].Heading);
        Assert.Equal("1. Ai chịu trách nhiệm về thông tin của bạn", Legal.Privacy.Sections[0].Heading);
        Assert.Equal("7. Quyền của bạn", Legal.Privacy.Sections[6].Heading);
        Assert.Equal(["Cập nhật lần cuối: 21/11/2026"], Legal.Terms.Lead);
        Assert.Equal(["Cập nhật lần cuối: 21/11/2026"], Legal.Privacy.Lead);
    }

    // The owner's order (2026-11-21): no placeholder text. A [BRACKET] left in a text would show on the page.
    [Fact]
    public void No_text_has_a_placeholder_in_brackets()
    {
        var texts = AboutTexts();
        foreach (var document in new[] { Legal.Terms, Legal.Privacy })
        {
            texts.AddRange(document.Lead);
            texts.AddRange(document.Sections.SelectMany(s => s.Paragraphs.Prepend(s.Heading)));
        }

        Assert.All(texts, text => Assert.DoesNotMatch(@"[\[\]]", text));
    }

    [Fact]
    public void No_Markdown_marks_are_left_in_any_text()
    {
        var texts = AboutTexts();
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

    // Every piece of text the About page shows.
    private static List<string> AboutTexts()
    {
        var texts = new List<string> { About.Title, About.CtaText, About.CtaButton };
        texts.AddRange(About.Intro);
        foreach (var section in About.Sections)
        {
            texts.Add(section.Heading);
            switch (section)
            {
                case AboutCards cards:
                    texts.AddRange(cards.Items.SelectMany(c => new[] { c.Title, c.Text }));
                    break;
                case AboutText text:
                    texts.AddRange(text.Paragraphs);
                    break;
                case AboutFaq faq:
                    texts.AddRange(faq.Questions.SelectMany(q => q.Answer.Prepend(q.Question)));
                    break;
            }
        }

        return texts;
    }

    [Fact]
    public void A_file_with_a_missing_part_is_refused_with_its_name_in_the_message()
    {
        var oneSection = Assert.Throws<InvalidOperationException>(() => AboutContent.Parse("# Tiếng Việt\n\n## A\nText\n", "about.md"));
        var noButton = Assert.Throws<InvalidOperationException>(() => AboutContent.Parse(
            "# Tiếng Việt\n\n## A\nText\n\n## B\n1. **T.** x\n\n**Khối kêu gọi:** only text\n", "about.md"));
        var mixed = Assert.Throws<InvalidOperationException>(() => AboutContent.Parse(
            "# Tiếng Việt\n\n## A\nText\n\n## B\n1. **T.** x\n\nA paragraph\n\n**Khối kêu gọi:** t | b\n", "about.md"));
        var noAnswer = Assert.Throws<InvalidOperationException>(() => AboutContent.Parse(
            "# Tiếng Việt\n\n## A\nText\n\n## B\n### A question?\n\n**Khối kêu gọi:** t | b\n", "about.md"));
        var noTitle = Assert.Throws<InvalidOperationException>(() => LegalContent.Parse("## 1. A\nText\n", "terms.vi.md"));
        var emptySection = Assert.Throws<InvalidOperationException>(() => LegalContent.Parse("# T\n\n## 1. A\n\n## 2. B\nText\n", "terms.vi.md"));

        Assert.Contains("about.md", oneSection.Message);
        Assert.Contains("Khối kêu gọi", noButton.Message);
        Assert.Contains("mixes numbered items", mixed.Message);
        Assert.Contains("has no answer", noAnswer.Message);
        Assert.Contains("terms.vi.md", noTitle.Message);
        Assert.Contains("each needs a paragraph", emptySection.Message);
    }
}

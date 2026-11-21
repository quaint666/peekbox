using System.Net;
using System.Text.RegularExpressions;
using Peekbox.Web.Content;

namespace Peekbox.Tests;

// The blog list and one post (F52, F53).
[Collection(DatabaseCollection.Name)]
public class BlogPageTests(PeekboxFixture fixture)
{
    private async Task<(HttpStatusCode Status, string Html)> GetAsync(string path)
    {
        var response = await fixture.Client().GetAsync(path);
        // Razor encodes Vietnamese letters as entities; decode them to compare text.
        // Whitespace is collapsed as a browser shows it (the views indent their markup).
        return (response.StatusCode, Regex.Replace(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), @"\s+", " "));
    }

    [Fact]
    public async Task The_list_shows_a_card_for_each_post_that_opens_the_post()
    {
        var (status, html) = await GetAsync("/blog");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<h1 class=\"blog__title\" id=\"blog-title\">Blog</h1>", html);
        Assert.Equal(6, html.Split("class=\"blog-card\"").Length - 1);
        Assert.Contains("href=\"/blog/chon-hop-qua-tet-theo-ngan-sach\"", html);
        Assert.Contains("<span class=\"blog-card__category\" id=\"category-chon-hop-qua-tet-theo-ngan-sach\">Mẹo chọn quà</span>", html);
        Assert.Contains("<span class=\"blog-card__title\" id=\"title-chon-hop-qua-tet-theo-ngan-sach\">Cách chọn hộp quà Tết theo ngân sách</span>", html);
        Assert.Contains("src=\"/images/blog/chon-hop-qua-tet-theo-ngan-sach.webp\"", html);
    }

    [Fact]
    public async Task A_card_link_is_named_by_its_title_and_described_by_its_category_and_text()
    {
        var (_, html) = await GetAsync("/blog");

        Assert.Contains("aria-labelledby=\"title-doc-han-su-dung-tren-bao-bi\" aria-describedby=\"category-doc-han-su-dung-tren-bao-bi excerpt-doc-han-su-dung-tren-bao-bi\"", html);
        Assert.Contains("id=\"excerpt-doc-han-su-dung-tren-bao-bi\"", html);
    }

    [Fact]
    public async Task The_list_marks_Blog_as_the_page_in_the_header()
    {
        var (_, html) = await GetAsync("/blog");

        Assert.Contains("<a href=\"/blog\" aria-current=\"page\">Blog</a>", html);
    }

    [Fact]
    public async Task A_post_shows_its_text_a_call_to_action_and_two_other_posts()
    {
        var (status, html) = await GetAsync("/blog/doc-han-su-dung-tren-bao-bi");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("<title>Cách đọc hạn sử dụng trên bao bì thực phẩm · Peekbox</title>", html);
        Assert.Contains("<h1 class=\"post__title\">Cách đọc hạn sử dụng trên bao bì thực phẩm</h1>", html);
        Assert.Contains("<strong>NSX hoặc Ngày sản xuất:</strong> ngày thực phẩm được làm ra.", html);
        Assert.Contains("<li><a href=\"/blog\">Blog</a></li>", html);   // the trail is the way back
        Assert.DoesNotContain("Quay lại Blog", html);
        Assert.Contains("href=\"/builder\">Tạo hộp quà</a>", html);
        Assert.Contains("Bài viết khác", html);
        Assert.Equal(2, html.Split("class=\"post-more__link\"").Length - 1);
        Assert.DoesNotContain("class=\"post-more__link\" href=\"/blog/doc-han-su-dung-tren-bao-bi\"", html);
    }

    [Fact]
    public async Task Every_post_opens()
    {
        var posts = BlogPosts.Load(Path.Combine(RepoRoot.Path, "content", "blog"));

        foreach (var post in posts.All)
        {
            var (status, html) = await GetAsync($"/blog/{post.Slug}");

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains($"<h1 class=\"post__title\">{post.Title}</h1>", html);
        }
    }

    [Fact]
    public async Task A_post_that_does_not_exist_is_a_404()
    {
        var (status, _) = await GetAsync("/blog/no-such-post");

        Assert.Equal(HttpStatusCode.NotFound, status);
    }
}

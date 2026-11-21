using Microsoft.AspNetCore.Mvc;
using Peekbox.Web.Content;
using Peekbox.Web.Models;
using Peekbox.Web.Navigation;

namespace Peekbox.Web.Controllers;

// The blog (F52, F53): the list of posts and one post. The posts are read once, at start (Content/BlogPosts.cs).
public class BlogController(BlogPosts posts) : Controller
{
    [HttpGet(SiteLinks.BlogPath)]
    [HttpHead(SiteLinks.BlogPath)]
    public IActionResult Index()
    {
        ViewData["Nav"] = SiteLinks.Blog.Key;
        return View(posts.All);
    }

    [HttpGet(SiteLinks.BlogPath + "/{slug}")]
    [HttpHead(SiteLinks.BlogPath + "/{slug}")]
    public IActionResult Post(string slug)
    {
        var post = posts.Find(slug);
        if (post is null)
        {
            return NotFound();
        }

        ViewData["Nav"] = SiteLinks.Blog.Key;
        return View(new BlogPostViewModel(post, posts.Related(post, RelatedCount)));
    }

    // "Bài viết khác" shows two posts (mockup 09).
    private const int RelatedCount = 2;
}

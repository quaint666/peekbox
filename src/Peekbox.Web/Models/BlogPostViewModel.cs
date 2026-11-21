using Peekbox.Web.Content;

namespace Peekbox.Web.Models;

// One blog post and the posts to read next.
public sealed record BlogPostViewModel(BlogPost Post, IReadOnlyList<BlogPost> Related);
